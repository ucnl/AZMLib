using System.Globalization;

namespace AZMLib
{
    public class AZMAntennaCorrector
    {
        private readonly List<CalibrationPoint> _calibrationTable;

        private struct CalibrationPoint
        {
            public double EncoderAngle;   // Угол энкодера (градусы, 0-360)
            public double Error;          // Ошибка = реальный_угол - угол_энкодера (градусы)

            public CalibrationPoint(double angle, double error)
            {
                EncoderAngle = angle;
                Error = error;
            }
        }

        public AZMAntennaCorrector()
        {
            _calibrationTable = new List<CalibrationPoint>();
        }

        public void LoadCalibration(double[] encoderAngles, double[] errors)
        {
            if (encoderAngles.Length != errors.Length)
                throw new ArgumentException("Lengths of the arrays must be the same");

            if (encoderAngles.Length < 2)
                throw new ArgumentException("Table should contain two points at least");

            _calibrationTable.Clear();

            for (int i = 0; i < encoderAngles.Length; i++)
            {
                // Normalizing angles to the range [0, 360)
                double normalizedAngle = NormalizeAngle(encoderAngles[i]);
                _calibrationTable.Add(new CalibrationPoint(normalizedAngle, errors[i]));
            }
            
            _calibrationTable.Sort((a, b) => a.EncoderAngle.CompareTo(b.EncoderAngle));

            // Virtual point to the end (360° = 0°)
            if (Math.Abs(_calibrationTable.Last().EncoderAngle - 360.0) > 0.01)
            {
                double firstError = _calibrationTable.First().Error;
                _calibrationTable.Add(new CalibrationPoint(360.0, firstError));
            }
        }
        
        public double CorrectAngle(double measuredAngle)
        {
            if (_calibrationTable.Count == 0)
                return measuredAngle;

            double normalizedAngle = NormalizeAngle(measuredAngle);

            double error = LinearInterpolation(normalizedAngle);

            double correctedAngle = normalizedAngle - error;
            
            return NormalizeAngle(correctedAngle);
        }

        private double LinearInterpolation(double angle)
        {
            int index = 0;
            while (index < _calibrationTable.Count && _calibrationTable[index].EncoderAngle <= angle)
            {
                index++;
            }

            if (index == 0)
                return _calibrationTable[0].Error;

            if (index >= _calibrationTable.Count)
                return _calibrationTable.Last().Error;

            var left = _calibrationTable[index - 1];
            var right = _calibrationTable[index];

            double t = (angle - left.EncoderAngle) / (right.EncoderAngle - left.EncoderAngle);
            return left.Error + t * (right.Error - left.Error);
        }        

        /// <summary>
        /// Angle normalizing to the range [0, 360)
        /// </summary>
        private double NormalizeAngle(double angle)
        {
            angle = angle % 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }
      
        public static (double[] angles, double[] errors) LoadFromFile(string filename)
        {
            var culture = CultureInfo.InvariantCulture;
            var angles = new List<double>();
            var errors = new List<double>();

            if (!System.IO.File.Exists(filename))
                throw new System.IO.FileNotFoundException($"Файл не найден: {filename}");

            var lines = System.IO.File.ReadAllLines(filename);

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(new[] { ';', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    throw new FormatException($"Строка {lineNum + 1}: ожидается 2 колонки");

                double angle = double.Parse(parts[0], culture);
                double error = double.Parse(parts[1], culture);
                angles.Add(angle);
                errors.Add(error);
            }

            if (angles.Count < 2)
                throw new FormatException($"Недостаточно точек: {angles.Count}");

            return (angles.ToArray(), errors.ToArray());
        }

        public static void SaveToFile(string filename, double[] angles, double[] errors)
        {
            var culture = CultureInfo.InvariantCulture;

            using (var writer = new System.IO.StreamWriter(filename))
            {
                writer.WriteLine("# Angle_deg;Error_deg");
                for (int i = 0; i < angles.Length; i++)
                {
                    writer.WriteLine($"{angles[i].ToString("F3", culture)};{errors[i].ToString("F6", culture)}");
                }
            }
        }
    }
}
