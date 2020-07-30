using System.Windows.Forms;

using GTA;

namespace FreecamV
{
    public class Main : Script
    {
        public Main()
        {
            // Load all of the config values from ini
            Config.DefaultSpeed = Settings.GetValue("Settings", "Speed", 4.0f);
            Config.ShiftSpeed = Settings.GetValue("Settings", "BoostSpeed", 20.0f);
            Config.FilterIntensity = Settings.GetValue("Settings", "FilterIntensity", 1.0f);
            Config.SlowMotionMultiplier = Settings.GetValue("Settings", "SlowMotionMult", 8.5f);
            Config.Precision = Settings.GetValue("Settings", "Precision", 1.0f); // This isnt actually in the ini on default but its still here for if I ever want to add it

            // Input handling
            KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Settings.GetValue("Keys", "Toggle", Keys.J))
                    Freecam.Toggle();
            };

            // Ticking for freecam
            Tick += (sender, e) =>
            {
                Freecam.Tick();
            };
        }
    }
}
