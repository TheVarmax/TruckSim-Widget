using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ETSOverlay
{
    /// <summary>
    /// Stable attributes of an order as the game reports them. They do not depend on the GPS route,
    /// on the planned distance or on TrucksBook, so the same order keeps the same fingerprint after
    /// a save is loaded, the order is paused or the route is rebuilt.
    /// </summary>
    internal sealed class DeliveryFingerprint
    {
        public string SourceCityId { get; set; } = "";
        public string DestinationCityId { get; set; } = "";
        public string SourceCompanyId { get; set; } = "";
        public string DestinationCompanyId { get; set; } = "";
        public string CargoId { get; set; } = "";
        /// <summary>Delivery deadline in game minutes; 0 when the game does not report one.</summary>
        public uint DeliveryTime { get; set; }
        public string Market { get; set; } = "";

        public static DeliveryFingerprint Create(string? sourceCityId, string? destinationCityId,
            string? sourceCompanyId, string? destinationCompanyId, string? cargoId, uint deliveryTime, string? market)
        {
            return new DeliveryFingerprint
            {
                SourceCityId = Normalize(sourceCityId),
                DestinationCityId = Normalize(destinationCityId),
                SourceCompanyId = Normalize(sourceCompanyId),
                DestinationCompanyId = Normalize(destinationCompanyId),
                CargoId = Normalize(cargoId),
                DeliveryTime = deliveryTime,
                Market = Normalize(market)
            };
        }

        private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

        /// <summary>
        /// Two cities alone never identify an order: the cargo and at least one company are required too.
        /// </summary>
        public bool IsComplete =>
            SourceCityId.Length > 0 && DestinationCityId.Length > 0 && CargoId.Length > 0
            && (SourceCompanyId.Length > 0 || DestinationCompanyId.Length > 0);

        public bool Matches(DeliveryFingerprint? other)
        {
            if (other == null || !IsComplete || !other.IsComplete) return false;
            if (SourceCityId != other.SourceCityId || DestinationCityId != other.DestinationCityId) return false;
            if (CargoId != other.CargoId) return false;
            if (!SameWhenKnown(SourceCompanyId, other.SourceCompanyId)) return false;
            if (!SameWhenKnown(DestinationCompanyId, other.DestinationCompanyId)) return false;
            if (!SameWhenKnown(Market, other.Market)) return false;
            // The deadline separates two orders between the same companies with the same cargo.
            if (DeliveryTime != 0 && other.DeliveryTime != 0 && DeliveryTime != other.DeliveryTime) return false;
            return true;
        }

        private static bool SameWhenKnown(string a, string b) => a.Length == 0 || b.Length == 0 || a == b;

        /// <summary>Short key for storage and logs. Contains no personal data.</summary>
        public string ToKey()
        {
            string raw = string.Join("|", SourceCityId, DestinationCityId, SourceCompanyId, DestinationCompanyId, CargoId, DeliveryTime, Market);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "fp_" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        }
    }

    /// <summary>What the identity resolver needs to know about a stored delivery.</summary>
    internal interface IDeliveryRecord
    {
        string RecordId { get; }
        IList<string> RecordAliases { get; }
        /// <summary>TrucksBook ids confirmed for this delivery. One delivery has one TrucksBook id.</summary>
        IList<string> RecordTrucksBookIds { get; }
        DeliveryFingerprint? RecordFingerprint { get; }
        string LegacyOrigin { get; }
        string LegacyDestination { get; }
        bool HasProgress { get; }
        DateTime RecordUpdatedUtc { get; }
    }

    internal enum DeliveryMatchKind
    {
        /// <summary>The telemetry belongs to a stored delivery.</summary>
        Existing,
        /// <summary>A delivery that has no stored record yet.</summary>
        New,
        /// <summary>Not enough attributes to tell: keep the snapshot and wait.</summary>
        Ambiguous
    }

    internal sealed class DeliveryMatch
    {
        public DeliveryMatchKind Kind { get; init; }
        public string Key { get; init; } = "";
        public string Reason { get; init; } = "";
        /// <summary>The confirmed TrucksBook id contradicts the order in the game and was not used.</summary>
        public bool TrucksBookIdRejected { get; init; }
    }

    internal sealed class DeliveryObservation
    {
        /// <summary>TrucksBook id, only while TrucksBook confirms an active delivery.</summary>
        public string ConfirmedTrucksBookId { get; init; } = "";
        public DeliveryFingerprint Fingerprint { get; init; } = new();
        /// <summary>Key of older versions: cities + planned distance.</summary>
        public string LegacyTelemetryId { get; init; } = "";
        public string SourceCityName { get; init; } = "";
        public string DestinationCityName { get; init; } = "";
        /// <summary>Record the widget currently tracks ("" when none).</summary>
        public string CurrentRecordId { get; init; } = "";
    }

    /// <summary>
    /// Maps the current telemetry to one stored delivery. The route, the planned distance and the
    /// source of the id may change; the record stays the same.
    /// </summary>
    internal static class DeliveryIdentityResolver
    {
        public static DeliveryMatch Resolve(IEnumerable<IDeliveryRecord> records, DeliveryObservation obs)
        {
            var all = records.ToList();
            var fp = obs.Fingerprint;
            string tbId = obs.ConfirmedTrucksBookId ?? "";
            bool tbRejected = false;

            // TrucksBook gives every delivery its own id. A record that already has a confirmed
            // TrucksBook id is therefore another delivery when a different id is confirmed now,
            // however similar the order looks (same companies and cargo, no deadline).
            bool BelongsToOtherTrucksBookDelivery(IDeliveryRecord r) =>
                tbId.Length > 0 && !tbRejected && r.RecordTrucksBookIds.Count > 0 && !HasId(r, tbId);

            // 1. A confirmed TrucksBook id is the strongest link, unless it still points at the
            //    previous order while the game already shows another one.
            if (tbId.Length > 0)
            {
                var byTb = all.FirstOrDefault(r => HasId(r, tbId));
                if (byTb != null)
                {
                    bool contradicts = fp.IsComplete && byTb.RecordFingerprint != null
                        && byTb.RecordFingerprint.IsComplete && !byTb.RecordFingerprint.Matches(fp);
                    if (!contradicts)
                        return new DeliveryMatch { Kind = DeliveryMatchKind.Existing, Key = byTb.RecordId, Reason = "trucksbook-id" };
                    tbRejected = true;
                }
            }

            // 2. Stable order attributes.
            if (fp.IsComplete)
            {
                var byFp = all.Where(r => r.RecordFingerprint != null && r.RecordFingerprint.Matches(fp)
                    && !BelongsToOtherTrucksBookDelivery(r)).ToList();
                if (byFp.Count > 0)
                {
                    var chosen = byFp.FirstOrDefault(r => r.RecordId == obs.CurrentRecordId)
                        ?? byFp.OrderByDescending(r => r.RecordUpdatedUtc).First();
                    return new DeliveryMatch { Kind = DeliveryMatchKind.Existing, Key = chosen.RecordId, Reason = "fingerprint", TrucksBookIdRejected = tbRejected };
                }
            }

            // 3. Records written by older versions have no fingerprint. They were keyed by the
            //    TrucksBook id or by cities + planned distance; adopt them once.
            var legacy = all.Where(r => r.RecordFingerprint == null && r.HasProgress
                && !BelongsToOtherTrucksBookDelivery(r)).ToList();
            if (legacy.Count > 0)
            {
                var byLegacyId = obs.LegacyTelemetryId.Length > 0
                    ? legacy.FirstOrDefault(r => HasId(r, obs.LegacyTelemetryId))
                    : null;
                if (byLegacyId != null)
                    return new DeliveryMatch { Kind = DeliveryMatchKind.Existing, Key = byLegacyId.RecordId, Reason = "legacy-telemetry-id", TrucksBookIdRejected = tbRejected };

                // The planned distance may have changed since the record was written: accept the
                // record the widget was tracking when its saved route is the one in the game.
                var current = legacy.FirstOrDefault(r => r.RecordId == obs.CurrentRecordId);
                if (current != null && SameRoute(current, obs))
                    return new DeliveryMatch { Kind = DeliveryMatchKind.Existing, Key = current.RecordId, Reason = "legacy-current-route", TrucksBookIdRejected = tbRejected };
            }

            if (!fp.IsComplete)
                return new DeliveryMatch { Kind = DeliveryMatchKind.Ambiguous, Reason = "incomplete-order-attributes", TrucksBookIdRejected = tbRejected };

            string key = tbId.Length > 0 && !tbRejected ? tbId : fp.ToKey();
            return new DeliveryMatch { Kind = DeliveryMatchKind.New, Key = key, Reason = "no-stored-record", TrucksBookIdRejected = tbRejected };
        }

        public static bool HasId(IDeliveryRecord record, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (string.Equals(record.RecordId, id, StringComparison.Ordinal)) return true;
            foreach (var alias in record.RecordAliases)
                if (string.Equals(alias, id, StringComparison.Ordinal)) return true;
            foreach (var tb in record.RecordTrucksBookIds)
                if (string.Equals(tb, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool SameRoute(IDeliveryRecord record, DeliveryObservation obs)
        {
            return record.LegacyOrigin.Length > 0 && record.LegacyDestination.Length > 0
                && string.Equals(record.LegacyOrigin, obs.SourceCityName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.LegacyDestination, obs.DestinationCityName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Why the odometer gave no distance for a pair of readings.</summary>
    internal enum OdometerDeltaResult
    {
        Counted,
        NoBase,
        Paused,
        NotMoved,
        WentBack,
        Gap,
        Teleport,
        Implausible
    }

    /// <summary>
    /// Counts the distance driven with the cargo from odometer readings. Only plausible forward
    /// steps inside one continuous stretch count; a step across a break (save load, pause,
    /// reconnect, teleport, ferry) counts as zero and the next reading becomes the new base.
    /// </summary>
    internal sealed class OdometerAccumulator
    {
        /// <summary>Longer silence between two readings is a break, not driving.</summary>
        public const long MaxSampleGapMs = 5000;
        /// <summary>Allowance over the reported speed, km/h.</summary>
        public const double SpeedMarginKmh = 40;
        /// <summary>Distance that is always accepted between two readings (rounding of the odometer), km.</summary>
        public const double MinAllowanceKm = 0.05;

        private double _lastOdometerKm = -1;
        private long _lastSampleMs;
        private double _lastSpeedKmh;

        public bool HasBase => _lastOdometerKm >= 0;
        public OdometerDeltaResult LastResult { get; private set; } = OdometerDeltaResult.NoBase;

        /// <summary>Forget the base: the next reading starts a new stretch and adds nothing.</summary>
        public void Break()
        {
            _lastOdometerKm = -1;
            _lastSpeedKmh = 0;
        }

        /// <summary>
        /// Feed one reading. Returns the kilometres to add (0 or more).
        /// </summary>
        /// <param name="counting">False while the delivery is not being driven (no cargo, paused order, unknown identity).</param>
        /// <param name="teleported">True when the truck position jumped since the previous reading.</param>
        public double Sample(double odometerKm, double speedKmh, long nowMs, bool counting, bool teleported = false)
        {
            speedKmh = Math.Abs(speedKmh);
            if (!counting || odometerKm <= 0 || double.IsNaN(odometerKm))
            {
                Break();
                LastResult = OdometerDeltaResult.Paused;
                return 0;
            }

            if (!HasBase)
            {
                SetBase(odometerKm, speedKmh, nowMs);
                LastResult = OdometerDeltaResult.NoBase;
                return 0;
            }

            double delta = odometerKm - _lastOdometerKm;
            long elapsedMs = nowMs - _lastSampleMs;
            double previousSpeed = _lastSpeedKmh;
            SetBase(odometerKm, speedKmh, nowMs);

            if (teleported) { LastResult = OdometerDeltaResult.Teleport; return 0; }
            if (delta < 0) { LastResult = OdometerDeltaResult.WentBack; return 0; }
            if (delta == 0) { LastResult = OdometerDeltaResult.NotMoved; return 0; }
            if (elapsedMs < 0 || elapsedMs > MaxSampleGapMs) { LastResult = OdometerDeltaResult.Gap; return 0; }

            double hours = Math.Max(elapsedMs, 1) / 3_600_000.0;
            double allowed = (Math.Max(speedKmh, previousSpeed) + SpeedMarginKmh) * hours * 1.5 + MinAllowanceKm;
            if (delta > allowed) { LastResult = OdometerDeltaResult.Implausible; return 0; }

            LastResult = OdometerDeltaResult.Counted;
            return delta;
        }

        private void SetBase(double odometerKm, double speedKmh, long nowMs)
        {
            _lastOdometerKm = odometerKm;
            _lastSpeedKmh = speedKmh;
            _lastSampleMs = nowMs;
        }
    }

    /// <summary>
    /// Keeps the total route length shown to the driver. It separates the original job distance,
    /// the distance already driven, the GPS remainder and the last trustworthy total, so a GPS
    /// that reports nothing or something outdated (menu, save load) does not change the total.
    /// All values are kilometres.
    ///
    /// After a break the GPS is trusted again only when it proves to be current: its remainder
    /// has to follow the truck (go down by about what was driven) over a short stretch. A value
    /// that merely stays the same, however long, proves nothing: that is what outdated data
    /// looks like. A zero remainder ends the route only when it lasts near the destination.
    /// </summary>
    internal sealed class RouteTotalEstimator
    {
        /// <summary>Distance the truck has to drive while the remainder follows it before the GPS is trusted again.</summary>
        public const double ConfirmDriveKm = 0.3;
        /// <summary>Allowed difference between "driven" and "remainder went down", fixed part, km.</summary>
        public const double FollowToleranceKm = 0.15;
        /// <summary>Allowed difference, part proportional to the distance driven.</summary>
        public const double FollowToleranceShare = 0.35;
        /// <summary>A zero remainder is the real finish only this close to the destination.</summary>
        public const double NearFinishKm = 1.5;
        /// <summary>...and only when it lasts this long (a single empty frame is not the finish).</summary>
        public const long FinishConfirmMs = 2000;
        /// <summary>A zero shorter than this is a blip: the GPS stays trusted when the value comes back.</summary>
        public const long BlipMs = 1000;

        private bool _gpsReady;
        private bool _hasCandidate;
        private double _candidateRemainingKm;
        private double _candidateDrivenKm;
        private long _zeroSinceMs = -1;
        private double _drivenAtLastPositiveKm;

        /// <summary>Distance of the job as first reported by the game.</summary>
        public double PlannedKm { get; private set; }
        /// <summary>Last trustworthy driven + GPS remainder; 0 when there has been none yet.</summary>
        public double LastTotalKm { get; private set; }
        /// <summary>Last trustworthy GPS remainder.</summary>
        public double LastRemainingKm { get; private set; }
        public bool GpsReady => _gpsReady;

        public void Reset()
        {
            PlannedKm = 0;
            LastTotalKm = 0;
            LastRemainingKm = 0;
            _gpsReady = false;
            _hasCandidate = false;
            _zeroSinceMs = -1;
            _drivenAtLastPositiveKm = 0;
        }

        /// <summary>Continue a stored delivery: the saved total is held until the GPS proves current.</summary>
        public void Restore(double plannedKm, double lastTotalKm, double lastRemainingKm)
        {
            PlannedKm = Sanitize(plannedKm);
            LastTotalKm = Sanitize(lastTotalKm);
            LastRemainingKm = Sanitize(lastRemainingKm);
            _gpsReady = false;
            _hasCandidate = false;
            _zeroSinceMs = -1;
        }

        /// <summary>A save was loaded, the game reconnected or the truck jumped: the GPS has to prove itself again.</summary>
        public void NotifyDiscontinuity()
        {
            _gpsReady = false;
            _hasCandidate = false;
            _zeroSinceMs = -1;
        }

        /// <summary>
        /// Feed one reading and get the total to show.
        /// </summary>
        /// <param name="drivenKm">Distance already counted for the delivery.</param>
        /// <param name="navRemainingKm">GPS remainder; 0 or less when the GPS reports nothing.</param>
        /// <param name="plannedKm">Planned distance the game reports now.</param>
        public double Update(double drivenKm, double navRemainingKm, double plannedKm, long nowMs)
        {
            drivenKm = Sanitize(drivenKm);
            navRemainingKm = Sanitize(navRemainingKm);
            plannedKm = Sanitize(plannedKm);

            if (PlannedKm <= 0 && plannedKm > 0) PlannedKm = plannedKm;

            if (navRemainingKm <= 0)
            {
                if (_zeroSinceMs < 0) _zeroSinceMs = nowMs;
                long zeroForMs = nowMs - _zeroSinceMs;

                if (_gpsReady && LastRemainingKm > 0)
                {
                    if (LastRemainingKm <= NearFinishKm)
                    {
                        // Near the destination: the route ends when the zero lasts, or when the
                        // truck has driven what was left. One empty frame changes nothing.
                        double drivenSince = drivenKm - _drivenAtLastPositiveKm;
                        bool arrived = zeroForMs >= FinishConfirmMs
                            || (drivenSince > 0 && drivenSince >= LastRemainingKm - FollowToleranceKm);
                        if (arrived)
                        {
                            LastRemainingKm = 0;
                            LastTotalKm = drivenKm;
                        }
                    }
                    else if (zeroForMs >= BlipMs)
                    {
                        // The GPS went silent away from the finish: hold the last estimate and
                        // make it prove itself when it is back.
                        _gpsReady = false;
                        _hasCandidate = false;
                    }
                }
            }
            else
            {
                _zeroSinceMs = -1;

                if (!_gpsReady)
                {
                    if (LastTotalKm <= 0)
                    {
                        // Nothing to protect yet (fresh order): the first route is the estimate.
                        _gpsReady = true;
                    }
                    else if (!_hasCandidate)
                    {
                        StartCandidate(navRemainingKm, drivenKm);
                    }
                    else
                    {
                        double driven = drivenKm - _candidateDrivenKm;
                        double wentDown = _candidateRemainingKm - navRemainingKm;
                        double tolerance = FollowToleranceKm + FollowToleranceShare * Math.Max(0, driven);

                        if (driven < 0 || Math.Abs(wentDown - driven) > tolerance)
                        {
                            // The remainder does not follow the truck (it jumped, or it stands
                            // still while the truck drives): not current. Start over from here.
                            StartCandidate(navRemainingKm, drivenKm);
                        }
                        else if (driven >= ConfirmDriveKm)
                        {
                            _gpsReady = true;
                            _hasCandidate = false;
                        }
                    }
                }

                if (_gpsReady)
                {
                    LastRemainingKm = navRemainingKm;
                    LastTotalKm = drivenKm + navRemainingKm;
                    _drivenAtLastPositiveKm = drivenKm;
                }
            }

            return CurrentTotal(drivenKm, plannedKm);
        }

        private void StartCandidate(double remainingKm, double drivenKm)
        {
            _hasCandidate = true;
            _candidateRemainingKm = remainingKm;
            _candidateDrivenKm = drivenKm;
        }

        /// <summary>Total to show without feeding a reading.</summary>
        public double CurrentTotal(double drivenKm, double plannedKm = 0)
        {
            double fallback = PlannedKm > 0 ? PlannedKm : Sanitize(plannedKm);
            double total = LastTotalKm > 0 ? LastTotalKm : fallback;
            return Math.Max(total, Sanitize(drivenKm));
        }

        private static double Sanitize(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? 0 : value;
    }

    /// <summary>What to do with a cancel event of the game.</summary>
    internal enum CancelDecision
    {
        /// <summary>Not clear yet whose event it is: keep the snapshot and wait.</summary>
        Pending,
        /// <summary>The tracked delivery was cancelled.</summary>
        Confirmed,
        /// <summary>The event does not belong to the tracked delivery: nothing changes.</summary>
        Rejected
    }

    /// <summary>
    /// Decides whether a cancel event of the game ends the delivery that is being tracked.
    /// The event itself says nothing about which order it means, so it is never applied at once:
    /// - a loaded save or a telemetry break around the event means the event belongs to the game
    ///   state that was just replaced: rejected, the snapshot stays;
    /// - if, in continuous telemetry after the event, the game still shows the tracked order as
    ///   active, the event was a late one about an earlier order: rejected;
    /// - if the game shows no job, or another order, the tracked delivery really was cancelled;
    /// - while it is unknown which order the game shows, the answer is "wait".
    /// </summary>
    internal sealed class CancelEventReview
    {
        /// <summary>A break this long before the event still explains it.</summary>
        public const long RecentBreakMs = 10000;
        /// <summary>Continuous telemetry needed after the event before deciding.</summary>
        public const long SettleMs = 1500;
        public const int SettleReadings = 5;

        private long _openedMs;
        private long _lastBreakMs = long.MinValue;
        private int _readings;

        public bool IsOpen { get; private set; }
        /// <summary>Record that was tracked when the event arrived.</summary>
        public string RecordId { get; private set; } = "";
        public string LastReason { get; private set; } = "";

        /// <summary>Every detected save load, telemetry gap, reconnect or jump of the truck.</summary>
        public void NoteBreak(long nowMs) => _lastBreakMs = nowMs;

        public void Open(string recordId, long nowMs)
        {
            IsOpen = true;
            RecordId = recordId ?? "";
            _openedMs = nowMs;
            _readings = 0;
            LastReason = "";
        }

        public void Close()
        {
            IsOpen = false;
            RecordId = "";
            _readings = 0;
        }

        /// <summary>
        /// One telemetry reading after the event.
        /// </summary>
        /// <param name="onJob">The game reports an active job.</param>
        /// <param name="trackedOrderShown">True: the game shows the tracked order. False: another order or none. Null: cannot tell yet.</param>
        public CancelDecision Observe(long nowMs, bool onJob, bool? trackedOrderShown)
        {
            if (!IsOpen) return CancelDecision.Rejected;

            if (_lastBreakMs != long.MinValue && _lastBreakMs >= _openedMs - RecentBreakMs)
            {
                LastReason = "save load or telemetry break around the event";
                Close();
                return CancelDecision.Rejected;
            }

            _readings++;
            if (nowMs - _openedMs < SettleMs || _readings < SettleReadings)
            {
                LastReason = "waiting for continuous telemetry";
                return CancelDecision.Pending;
            }

            if (onJob && trackedOrderShown == true)
            {
                LastReason = "the tracked delivery is still active in the game";
                Close();
                return CancelDecision.Rejected;
            }

            if (!onJob || trackedOrderShown == false)
            {
                LastReason = onJob ? "the game shows another order" : "the game shows no job";
                Close();
                return CancelDecision.Confirmed;
            }

            LastReason = "cannot tell which order the game shows";
            return CancelDecision.Pending;
        }
    }

    /// <summary>Unit handling of stored delivery distances.</summary>
    internal static class DeliveryUnits
    {
        public const double KmToMiles = 0.621371;

        /// <summary>
        /// Older snapshots stored the driven distance in the unit shown in the game:
        /// kilometres for ETS2, miles for ATS.
        /// </summary>
        public static double LegacyDrivenToKm(double legacyDrivenDistance, bool isAts)
        {
            if (double.IsNaN(legacyDrivenDistance) || legacyDrivenDistance <= 0) return 0;
            return isAts ? legacyDrivenDistance / KmToMiles : legacyDrivenDistance;
        }

        public static double KmToDisplay(double km, bool useMiles) => useMiles ? km * KmToMiles : km;
    }
}
