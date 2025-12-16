using System.Text.Json.Serialization;

namespace LoginAppFramework
{
    public class ConnectionSettings
    {
        public string ServerAddress { get; set; }
        public string SqlUsername { get; set; }

        // This is the property that will be stored in the settings file
        public string EncryptedSqlPassword { get; set; }

        // This is a temporary, in-memory property that will NOT be saved to the file
        [JsonIgnore]
        public string SqlPassword { get; set; }
    }
}