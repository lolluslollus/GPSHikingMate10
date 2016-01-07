using System;
using System.Diagnostics;
using Windows.Storage;

namespace Utilz
{
    public static class RegistryAccess
    {
        // If settings.Values[regKey] is not found when reading, it returns null but does not throw. 
        // If it is not found when writing, it creates the key

        public static string ReadAllReg() //LOLLO this is only for testing or diagnosing
        {
            var settings = ApplicationData.Current.LocalSettings;
            string output = string.Empty;
            foreach (var item in settings.Values)
            {
                Debug.WriteLine(item.Key + " = " + item.Value.ToString());
                output += (item.Key + " = " + item.Value.ToString() + System.Environment.NewLine);
            }
            return output;
        }

        public static void SetValue(string regKey, string value)
        {
            Debug.WriteLine("writing value " + value + " into reg key " + regKey);
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[regKey] = value;
        }

        public static string GetValue(string regKey)
        {
            var settings = ApplicationData.Current.LocalSettings;
            string valueStr = string.Empty;
            Object valueObj = settings.Values[regKey];
            if (valueObj != null)
            {
                valueStr = valueObj.ToString();
            }
            Debug.WriteLine("reg key " + regKey + " has value " + valueStr);
            return valueStr;
        }
    }
}
