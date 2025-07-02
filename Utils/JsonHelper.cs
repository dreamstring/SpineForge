using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpineForge.Utils
{
    public static class JsonHelper
    {
        public static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            
            PropertyNamingPolicy = null,
            IncludeFields = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            // 确保枚举正确序列化
            Converters = { new JsonStringEnumConverter() }
        };
    }
}