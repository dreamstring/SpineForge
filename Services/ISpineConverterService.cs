using SpineForge.Models;
using System;
using System.Threading.Tasks;

namespace SpineForge.Services;

public interface ISpineConverterService
{
    Task<bool> ConvertAsync(SpineAsset asset, ConversionSettings settings, IProgress<string>? progress = null);
    bool ValidateSpineExecutable(string? path);
    string GetSpineVersion(string? executablePath);
}