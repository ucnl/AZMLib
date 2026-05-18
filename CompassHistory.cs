using System.Collections.Generic;
using System.Diagnostics;

namespace AZMLib
{
    internal class CompassHistory
    {
        private readonly List<CompassPoint> _points = new();
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        // Буфер на 4 секунды (при 1 Гц — 4-5 точек, при 5 Гц — 20 точек)
        private const double BUFFER_SECONDS = 4.0;

        // Максимальная экстраполяция в будущее (мс)
        private const double MAX_EXTRAPOLATION_MS = 500.0;

        // Интервал, дольше которого интерполяция считается ненадёжной
        private const double RELIABLE_INTERVAL_MS = 1500.0;

        public void Add(double heading)
        {
            _points.Add(new CompassPoint { Ticks = _sw.ElapsedTicks, Heading = heading });

            long cutoff = _sw.ElapsedTicks - (long)(BUFFER_SECONDS * Stopwatch.Frequency);
            while (_points.Count > 1 && _points[0].Ticks < cutoff)
                _points.RemoveAt(0);
        }

        public double GetHeadingAtOffset(double offsetMs)
        {
            long targetTicks = _sw.ElapsedTicks -
                (long)(offsetMs / 1000.0 * Stopwatch.Frequency);

            return Interpolate(targetTicks);
        }

        private double Interpolate(long targetTicks)
        {
            if (_points.Count == 0)
                return 0;

            if (_points.Count == 1)
                return _points[0].Heading;

            // Ищем интервал, содержащий target
            for (int i = 0; i < _points.Count - 1; i++)
            {
                if (targetTicks >= _points[i].Ticks && targetTicks <= _points[i + 1].Ticks)
                {
                    long interval = _points[i + 1].Ticks - _points[i].Ticks;
                    double intervalMs = (double)interval / Stopwatch.Frequency * 1000.0;

                    // Если интервал слишком большой — всё равно интерполируем,
                    // но результат будет менее точным при нелинейном вращении
                    if (intervalMs > RELIABLE_INTERVAL_MS)
                    {
                        // Компас, возможно, завис — возвращаем ближайшую точку
                        long distToI = targetTicks - _points[i].Ticks;
                        long distToI1 = _points[i + 1].Ticks - targetTicks;
                        return distToI <= distToI1 ? _points[i].Heading : _points[i + 1].Heading;
                    }

                    double frac = (double)(targetTicks - _points[i].Ticks) / interval;
                    double delta = AngleDiff(_points[i].Heading, _points[i + 1].Heading);
                    return NormalizeAngle(_points[i].Heading + delta * frac);
                }
            }

            // Цель левее первой точки (далёкое прошлое)
            if (targetTicks < _points[0].Ticks)
            {
                return _points[0].Heading;
            }

            // Цель правее последней точки (будущее) — экстраполяция
            int last = _points.Count - 1;
            long dtTicks = _points[last].Ticks - _points[last - 1].Ticks;
            double extrapMs = (double)(targetTicks - _points[last].Ticks) / Stopwatch.Frequency * 1000.0;

            if (extrapMs > MAX_EXTRAPOLATION_MS || dtTicks == 0)
            {
                // Слишком далеко в будущее — возвращаем последнее измерение
                return _points[last].Heading;
            }

            // Экстраполяция в пределах допустимого
            double deltaLast = AngleDiff(_points[last - 1].Heading, _points[last].Heading);
            double rate = deltaLast / ((double)dtTicks / Stopwatch.Frequency); // °/с
            double extrapDelta = rate * (extrapMs / 1000.0);

            return NormalizeAngle(_points[last].Heading + extrapDelta);
        }

        private static double AngleDiff(double a, double b)
        {
            double diff = b - a;
            while (diff > 180) diff -= 360;
            while (diff < -180) diff += 360;
            return diff;
        }

        private static double NormalizeAngle(double a)
        {
            a %= 360;
            if (a < 0) a += 360;
            return a;
        }

        private struct CompassPoint
        {
            public long Ticks;
            public double Heading;
        }
    }
}