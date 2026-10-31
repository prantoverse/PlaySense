using System;

namespace PlaySense.Data.Models
{
    [Serializable]
    public class EventParameter
    {
        public string Key = string.Empty;

        public string Value = string.Empty;

        public EventParameter()
        {
        }

        public EventParameter(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }
}
