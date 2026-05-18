// AZMLib/AuxDataProvider.cs
using UCNLDrivers;
using UCNLDrivers.uAux;

namespace AZMLib
{
    /// <summary>
    /// Агрегирует данные от всех зарегистрированных IAuxSource.
    /// Знает про конкретные типы: uAuxGNSSPort, uAuxBPPort, 
    /// uAuxRadantPort.
    /// Предоставляет единые свойства для AZMCombiner.
    /// </summary>
    public class AuxDataProvider
    {
        private readonly AuxManager _auxManager;

        // ======== АГРЕГИРОВАННЫЕ ДАННЫЕ ========

        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }
        public double? Heading { get; private set; }
        public bool? IsHeadingTrue { get; private set; }
        public double? Course { get; private set; }
        public double? Speed { get; private set; }
        public double? Depth { get; private set; }

        public double? CurrentRotatorAngle { get; private set; }
        public bool RotatorBusy { get; private set; }

        public DateTime LastUpdate { get; private set; } = DateTime.MinValue;

        public bool HasValidPosition =>
            Latitude.HasValue && Longitude.HasValue;

        // ======== СОБЫТИЯ ========

        public event EventHandler? OnDataUpdated;
        public event EventHandler? OnRotatorUpdated;

        // ======== КОНСТРУКТОР ========

        public AuxDataProvider(AuxManager auxManager)
        {
            _auxManager = auxManager ?? throw new ArgumentNullException(nameof(auxManager));
            _auxManager.OnSourceStatusChanged += OnSourceStatusChanged;
        }

        // ======== ПОДПИСКА НА ИСТОЧНИКИ ========

        private void OnSourceStatusChanged(object? sender, AuxSourceStatusEventArgs e)
        {
            // Подписываемся только когда источник стал Detected
            if (e.Info.Status != AuxStatus.Detected) return;

            var source = _auxManager.GetSource(e.Info.Id);
            if (source == null) return;

            SubscribeToSource(source);
        }

        /// <summary>
        /// Можно вызвать принудительно для уже активных источников.
        /// </summary>
        public void SubscribeAll()
        {
            foreach (var info in _auxManager.GetAllSources())
            {
                if (info.Status == AuxStatus.Detected)
                {
                    var source = _auxManager.GetSource(info.Id);
                    if (source != null)
                        SubscribeToSource(source);
                }
            }
        }

        private void SubscribeToSource(IAuxSource source)
        {
            switch (source)
            {
                case uAuxGNSSPort gnss:
                    gnss.HeadingUpdated += (_, _) => UpdateFromGNSS(gnss);
                    gnss.LocationUpdated += (_, _) => UpdateFromGNSS(gnss);
                    UpdateFromGNSS(gnss); // сразу взять текущие
                    break;

                case uAuxBPPort bp:
                    bp.LocationUpdated += (_, _) => UpdateFromBP(bp);
                    UpdateFromBP(bp);
                    break;                

                case uAuxRadantPort radant:
                    radant.CurrentAngleChanged += (_, _) => UpdateFromRadant(radant);
                    radant.BusyChanged += (_, _) => UpdateFromRadant(radant);
                    UpdateFromRadant(radant);
                    break;
            }
        }

        // ======== ОБРАБОТЧИКИ ДАННЫХ ========

        private void UpdateFromGNSS(uAuxGNSSPort gnss)
        {
            if (!double.IsNaN(gnss.Heading))
            {
                Heading = gnss.Heading;
                IsHeadingTrue = gnss.IsHeadingTrue;
            }
            if (!double.IsNaN(gnss.Latitude) && !double.IsNaN(gnss.Longitude))
            {
                Latitude = gnss.Latitude;
                Longitude = gnss.Longitude;
            }
            if (!double.IsNaN(gnss.CourseOverGround))
                Course = gnss.CourseOverGround;
            if (!double.IsNaN(gnss.GroundSpeed))
                Speed = gnss.GroundSpeed;

            LastUpdate = DateTime.UtcNow;
            OnDataUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFromBP(uAuxBPPort bp)
        {
            if (!double.IsNaN(bp.Latitude) && !double.IsNaN(bp.Longitude))
            {
                Latitude = bp.Latitude;
                Longitude = bp.Longitude;
            }
            if (!double.IsNaN(bp.CourseOverGround))
                Course = bp.CourseOverGround;
            if (!double.IsNaN(bp.GroundSpeed))
                Speed = bp.GroundSpeed;

            LastUpdate = DateTime.UtcNow;
            OnDataUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFromRadant(uAuxRadantPort radant)
        {
            if (!double.IsNaN(radant.CurrentAngle))
                CurrentRotatorAngle = radant.CurrentAngle;
            RotatorBusy = radant.Busy;
            OnRotatorUpdated?.Invoke(this, EventArgs.Empty);
        }

        // ======== ДОСТУП К ИСТОЧНИКАМ ========

        public uAuxGNSSPort? FindGNSSPort()
        {
            foreach (var info in _auxManager.GetAllSources())
            {
                var source = _auxManager.GetSource(info.Id);
                if (source is uAuxGNSSPort gnss)
                    return gnss;
            }
            return null;
        }

        public uAuxBPPort? FindBPPort()
        {
            foreach (var info in _auxManager.GetAllSources())
            {
                var source = _auxManager.GetSource(info.Id);
                if (source is uAuxBPPort bp)
                    return bp;
            }
            return null;
        }

        public uAuxRadantPort? FindRadantPort()
        {
            foreach (var info in _auxManager.GetAllSources())
            {
                var source = _auxManager.GetSource(info.Id);
                if (source is uAuxRadantPort radant)
                    return radant;
            }
            return null;
        }
    }
}