using System;

namespace PlaySense.Behaviour
{
    [Serializable]
    public class PlaySenseFinding
    {
        public string Type;
        public string Text;
        public float Timestamp;

        public PlaySenseFinding(
            string type,
            string text,
            float timestamp = 0f)
        {
            Type = type;
            Text = text;
            Timestamp = timestamp;
        }
    }
}