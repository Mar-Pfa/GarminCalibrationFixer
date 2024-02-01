using System.Reflection;
using Dynastream.Fit;

namespace GarminCalibrationFixer
{
    public class MessageDefinitions
    {
        private static Dictionary<int, string> _names;

        public static Dictionary<int, string> Names
        {
            get
            {
                if (_names != null)
                    return _names;
                _names = new Dictionary<int, string>();
                foreach (FieldInfo field in typeof(MesgNum).GetFields())
                {
                    object obj = field.GetValue(null);
                    if (obj != null && obj is ushort)
                        _names.Add((UInt16) field.GetValue(null), field.Name);
                }
                return _names;
            }
        }
    }
}
