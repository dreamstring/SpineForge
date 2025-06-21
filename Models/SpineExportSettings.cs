// Models/SpineExportSettings.cs
using System.Text.Json.Serialization;

namespace SpineForge.Models
{
    public class SpineExportSettings
    {
        [JsonPropertyName("spine")]
        public SpineSettings Spine { get; set; } = new();
    }

    public class SpineSettings
    {
        [JsonPropertyName("export")]
        public ExportSettings Export { get; set; } = new();
    }

    public class ExportSettings
    {
        [JsonPropertyName("json")]
        public JsonExportSettings Json { get; set; } = new();

        [JsonPropertyName("binary")]
        public BinaryExportSettings Binary { get; set; } = new();

        [JsonPropertyName("texture")]
        public TextureExportSettings Texture { get; set; } = new();
    }

    public class JsonExportSettings
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = "";

        [JsonPropertyName("pretty")]
        public bool Pretty { get; set; } = true;

        [JsonPropertyName("nonessential")]
        public bool Nonessential { get; set; } = false;

        [JsonPropertyName("minify")]
        public bool Minify { get; set; } = false;
    }

    public class BinaryExportSettings
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = "";

        [JsonPropertyName("nonessential")]
        public bool Nonessential { get; set; } = false;
    }

    public class TextureExportSettings
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = "";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "json";

        [JsonPropertyName("pot")]
        public bool Pot { get; set; } = false;

        [JsonPropertyName("bleed")]
        public bool Bleed { get; set; } = true;

        [JsonPropertyName("premultiplyAlpha")]
        public bool PremultiplyAlpha { get; set; } = false;

        [JsonPropertyName("stripWhitespace")]
        public bool StripWhitespace { get; set; } = true;

        [JsonPropertyName("rotation")]
        public bool Rotation { get; set; } = false;

        [JsonPropertyName("flattenPaths")]
        public bool FlattenPaths { get; set; } = false;

        [JsonPropertyName("square")]
        public bool Square { get; set; } = false;

        [JsonPropertyName("filterMag")]
        public string FilterMag { get; set; } = "linear";

        [JsonPropertyName("filterMin")]
        public string FilterMin { get; set; } = "linear";

        [JsonPropertyName("wrapX")]
        public string WrapX { get; set; } = "clampToEdge";

        [JsonPropertyName("wrapY")]
        public string WrapY { get; set; } = "clampToEdge";

        [JsonPropertyName("alias")]
        public bool Alias { get; set; } = true;

        [JsonPropertyName("ignoreBlankImages")]
        public bool IgnoreBlankImages { get; set; } = true;

        [JsonPropertyName("limitMemory")]
        public bool LimitMemory { get; set; } = true;

        [JsonPropertyName("maxWidth")]
        public int MaxWidth { get; set; } = 2048;

        [JsonPropertyName("maxHeight")]
        public int MaxHeight { get; set; } = 2048;

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 1.0;

        [JsonPropertyName("scaleSuffix")]
        public string ScaleSuffix { get; set; } = "";

        [JsonPropertyName("atlasExtension")]
        public string AtlasExtension { get; set; } = ".atlas";
    }
}
