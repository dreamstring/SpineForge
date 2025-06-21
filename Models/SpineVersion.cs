namespace SpineForge.Models
{
    public class SpineVersion
    {
        public string Version { get; set; }
        public string Name { get; set; }

        public SpineVersion(string version, string name)
        {
            Version = version;
            Name = name;
        }

        public override string ToString() => Name;
    }
}