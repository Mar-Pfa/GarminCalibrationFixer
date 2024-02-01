using System.Globalization;
using Dynastream.Fit;
using System.IO.Compression;
using System.Text;


#nullable enable
namespace GarminCalibrationFixer
{
    public class Program
    {
        private static bool _debug;
        private static bool _heightProvided;
        private static int _heightMeter;
        private static int _heightPercentage;

        private static readonly Dictionary<ushort, List<string>> DistanceUpdates = new()
        {
            { 20, new List<string> { "Distance", "EnhancedSpeed", "StepLength" } },
            { 18, new List<string> { "AvgStepLength" } },
            { 313, new List<string> { "TotalDistance", "AvgSpeed", "MaxSpeed" } },
            { 312, new List<string> { "TotalDistance", "AvgSpeed", "MaxSpeed" } },
            { 19, new List<string> { "TotalDistance", "EnhancedAvgSpeed", "EnhancedMaxSpeed", "AvgStepLength" } }
        };

        private static readonly Dictionary<ushort, List<string>> PowerUpdates = new()
        {
            { 20, new List<string> { "Power", "AccumulatedPower" } },
            { 18, new List<string> { "TrainingLoadPeak", "AvgPower", "MaxPower", "NormalizedPower", "TotalWork", "TotalTrainingEffect", "TotalCalories" } },
            { 313, new List<string> { "TotalCalories" } },
            { 312, new List<string> { "TotalCalories" } },
            { 19, new List<string> { "AvgPower", "MaxPower", "NormalizedPower", "TotalWork", "TotalCalories" } }
        };

        private static readonly List<Mesg> Messages = new();

        /*
        private List<int> _ignorableMessages = new()
        {
            288,
            327,
            326,
            21,
            23,
            22,
            141,
            394,
            2,
            3,
            147,
            79,
            12,
            13,
            7,
            20,
            216,
            113,
            34,
            313,
            312,
            19
        };
        */

        private static void Main(string[] args)
        {
            List<string> stringList = new List<string>();
            foreach (string path in args)
            {
                if (System.IO.File.Exists(path))
                    stringList.Add(path);
                if (path.StartsWith("-h"))
                {
                    string[] strArray = path.Split('=');
                    if (strArray.Length == 2)
                        Program.ParseHeight(strArray[1]);
                }

                if (path == "-d")
                    Program._debug = true;
                else if (path == "-?")
                {
                    Console.WriteLine("Parameters:");
                    Console.WriteLine("-?      -> print info");
                    Console.WriteLine("-d      -> debug info -> output details for the processes files to console");
                    Console.WriteLine("-h=123m -> add 123 height meters to the run (and this increases the power, calories, ...)");
                    Console.WriteLine("-h=10%  -> add 10% height gradient to the run (and this increases the power, calories, ...)");
                    Console.WriteLine("[file1.fit] [file2.fit] ...  -> process all files. new filename is always [file1].new.fit [file2].new.fit");
                }
            }

            if (!_heightProvided)
            {
                Console.WriteLine("enter a height value (e.g. 100m or 5%) to add or leave empty if not needed");
                string heightInputString = Console.ReadLine()??string.Empty;
                if (!string.IsNullOrEmpty(heightInputString))
                    ParseHeight(heightInputString);
            }

            foreach (string str in stringList)
            {
                string fullPath = Path.GetFullPath(str);
                string withoutExtension = Path.GetFileNameWithoutExtension(str);
                string extension = Path.GetExtension(str);
                if (extension.ToUpper() == ".ZIP")
                {
                    string outputFileName = Path.Combine(fullPath.Substring(0, fullPath.Length - withoutExtension.Length - extension.Length), withoutExtension + ".new.fit");
                    ProcessZip(str, outputFileName, _debug);
                }
                else if (extension.ToUpper() == ".FIT")
                {
                    string outputFileName = Path.Combine(fullPath.Substring(0, fullPath.Length - withoutExtension.Length - extension.Length), withoutExtension + ".new.fit");
                    ProcessFit(str, outputFileName, _debug);
                }
                else
                    Console.WriteLine("don't know how to handle files with extension '" + extension + "' please provide a .zip or a .fit ");
            }

            Console.WriteLine("press a key to continue");
            Console.ReadKey();
        }

        private static void ParseHeight(string heightInputString)
        {
            string s = heightInputString.Length > 1 ? heightInputString.Substring(0, heightInputString.Length - 1) : string.Empty;
            if (heightInputString.EndsWith("m"))
            {
                if (Program._heightPercentage > 0)
                {
                    Console.WriteLine("cannot provide height meter and percentage at the same time! First value wins");
                }
                else
                {
                    if (!int.TryParse(s, out Program._heightMeter))
                        return;
                    Console.WriteLine("adding additional " + Program._heightMeter.ToString() + " height meters");
                    Program._heightProvided = true;
                }
            }
            else
            {
                if (!heightInputString.EndsWith("%"))
                    return;
                if (Program._heightMeter > 0)
                    Console.WriteLine("cannot provide height meter and percentage at the same time! First value wins");
                else if (int.TryParse(s, out Program._heightPercentage))
                {
                    Console.WriteLine("adding additional " + Program._heightPercentage.ToString() + " height%");
                    Program._heightProvided = true;
                }
            }
        }

        private static void UpdateField(Mesg message, byte fieldNum, float factor)
        {
            Field field = message.GetField(fieldNum);
            if (field == null)
                return;
            object obj = field.GetValue();
            if (obj is float num4)
            {
                float num = num4 * factor;
                field.SetValue(num);
            }
            else if (obj is ushort num3)
            {
                double num = num3 * 1.0 * factor;
                field.SetValue((ushort)num);
            }
            else
            {
                if (!(obj is uint num1))
                    throw new Exception("unsupported type in UpdateField: " + obj.GetType());
                double num2 = num1 * 1.0 * factor;
                field.SetValue((uint)num2);
            }
        }

        private static void UpdateField(Mesg message, string fieldName, float factor)
        {
            Field field = message.GetField(fieldName);
            if (field == null)
                return;
            object obj = field.GetValue();
            if (obj is float num4)
            {
                float num = num4 * factor;
                field.SetValue(num);
            }
            else if (obj is ushort num3)
            {
                double num = num3 * 1.0 * factor;
                field.SetValue((ushort)num);
            }
            else
            {
                if (!(obj is uint num1))
                    throw new Exception("unsupported type in UpdateField: " + obj.GetType());
                double num2 = num1 * 1.0 * factor;
                field.SetValue((uint)num2);
            }
        }

        private static void ProcessFit(string inputFileName, string outputFileName, bool printDebug)
        {
            using FileStream inputStream = new FileStream(inputFileName, FileMode.Open);
            Process(inputStream, outputFileName, printDebug);
        }

        private static void ProcessZip(string inputFileName, string outputFileName, bool printDebug)
        {
            ZipArchiveEntry source = ZipFile.Open(inputFileName, ZipArchiveMode.Read).Entries.First();
            string str = Path.GetTempPath() + source.FullName;
            source.ExtractToFile(str, true);
            ProcessFit(str, outputFileName, printDebug);
            System.IO.File.Delete(str);
        }

        private static string GetString(Field field)
        {
            var text = Encoding.UTF8.GetString((byte[])field.GetValue());
            if (text.EndsWith('\0'))
                text = text.Substring(0, text.Length - 1); 
            return text;
        }

        private static void Process(Stream inputStream, string outputFileName, bool printDebug)
        {
            Decode decode = new Decode();
            decode.MesgEvent += Decoder_MessageEvent;
            decode.Read(inputStream);
            if (printDebug)
                DebugOutput(Messages);

            var infolabel = "recalibrated";
            var workoutMessage = Messages.LastOrDefault(a => a.Num == 26);
            if (workoutMessage != null)
            {
                var workoutMessageField = workoutMessage.GetField("WktName");
                var workoutMessageString = GetString(workoutMessageField);
                
                if (!workoutMessageString.Contains(infolabel))
                {
                    workoutMessageString += " - " + infolabel + '\0';
                    workoutMessageField.SetValue(Encoding.ASCII.GetBytes(workoutMessageString));
                }
            }

            float num1 = (float)Messages.Last(a => a.Num == 20).GetField("Distance").GetValue();
            Console.WriteLine("Recorded Distance: " + num1.ToString(CultureInfo.InvariantCulture));
            float num2 = (float)Program.Messages.Last(a => a.Num == 18).GetField("TotalDistance").GetValue();
            Console.WriteLine("Session Distance: " + num2.ToString(CultureInfo.InvariantCulture));
            float num3 = (float)Program.Messages.Last(a => a.Num == 18).GetField("TotalElapsedTime").GetValue();
            if (Math.Abs(num2 - num1) < 0.1 && _heightMeter == 0)
            {
                Console.WriteLine("nothing to calibrate!");
            }
            else
            {
                float factor1 = num2 / num1;
                Console.WriteLine("distance correction: " + factor1);
                float factor2 = 0.0f;
                if (_heightMeter != 0)
                {
                    factor2 = (float)(1.0 + (_heightMeter * 100) / (double)num2 * 0.045000001788139343);
                    Console.WriteLine("height correction " + factor2);
                }
                else if (_heightPercentage != 0)
                {
                    factor2 = (float)(1.0 + _heightPercentage * 0.045000001788139343);
                    Console.WriteLine("height correction " + factor2.ToString(CultureInfo.InvariantCulture));
                }

                foreach (Mesg message in Messages)
                {
                    if (message.Num == 18)
                        Thread.Sleep(0);
                    if (DistanceUpdates.ContainsKey(message.Num))
                    {
                        foreach (string fieldName in DistanceUpdates[message.Num])
                            UpdateField(message, fieldName, factor1);
                    }

                    if (factor2 > 1.0 && PowerUpdates.ContainsKey(message.Num))
                    {
                        foreach (string fieldName in Program.PowerUpdates[message.Num])
                        {
                            UpdateField(message, fieldName, factor1);
                            UpdateField(message, fieldName, factor2);
                        }
                    }

                    if (message.Num == 18)
                    {
                        UpdateField(message, 178, factor1);
                        UpdateField(message, 178, factor2);
                    }
                }

                Encode encode = new Encode(ProtocolVersion.V20);
                using (FileStream fitDest = new FileStream(outputFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                {
                    encode.Open(fitDest);
                    encode.Write(Messages);
                    encode.Close();
                    fitDest.Close();
                }

                float num4 = (float)(num3 / 60.0 / (num2 / 1000.0));
                int num5 = (int)num4;
                float num6 = (float)(num4 * 60.0 % 60.0);
                Console.WriteLine("new Pace: " + num5 + ":" + num6.ToString("00"));
                if (factor2 > 1.0)
                {
                    float num7 = num4 / factor2;
                    int num8 = (int)num7;
                    float num9 = (float)(num7 * 60.0 % 60.0);
                    Console.WriteLine("new calculated Pace respecting gradient " + num8 + ":" + num9.ToString("00"));
                }

                Console.WriteLine("converted to " + outputFileName);
            }
        }

        private static void DebugOutput(List<Mesg> messages)
        {
            foreach (Mesg message in messages)
            {
                Console.WriteLine((MessageDefinitions.Names.ContainsKey(message.Num) ? MessageDefinitions.Names[message.Num] : "unknown ") + " (" + message.Num.ToString() + ")");
                foreach (Field field in message.Fields)
                {
                    object bytes = field.GetValue();
                    if (bytes.GetType() == typeof(byte[]))
                    {
                        string str = Encoding.UTF8.GetString((byte[])bytes);
                        if (str.EndsWith("\0"))
                            str = str.Substring(0, str.Length - 1);
                        bytes = str;
                    }

                    string str1 = field.Name;
                    if (str1 == "unknown")
                    {
                        byte num = field.Num;
                        string str2 = num.ToString();
                        num = field.Type;
                        string str3 = num.ToString();
                        str1 = "unknown Num " + str2 + " Type " + str3;
                        Thread.Sleep(0);
                    }

                    Console.WriteLine(" ->" + str1 + " = " + bytes);
                }
            }
        }

        private static void Decoder_MessageEvent(object sender, MesgEventArgs e) => Program.Messages.Add(e.mesg);
    }
}
