using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpineForge.Models;
using Microsoft.Win32;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace SpineForge.Services
{
    public class SpineConverterService : ISpineConverterService
    {
        private readonly List<string> _tempFiles = [];

        public async Task<bool> ConvertSpineFilesAsync(
            string? spineExePath,
            List<string>? spineFiles,
            ConversionSettings settings,
            IProgress<string>? progress = null)
        {
            try
            {
                // 1. 首先验证传入的路径
                progress?.Report($"DEBUG: 传入的 spineExePath = '{spineExePath ?? "NULL"}'");

                string? validSpineExePath = null;

                // 2. 如果传入路径有效，使用传入路径
                if (!string.IsNullOrEmpty(spineExePath))
                {
                    string? cleanPath = spineExePath.Trim('"');
                    if (File.Exists(cleanPath) && ValidateSpineExecutable(cleanPath))
                    {
                        validSpineExePath = cleanPath;
                        progress?.Report($"使用传入的 Spine 路径: {validSpineExePath}");
                    }
                }

                // 3. 如果传入路径无效，尝试自动查找
                if (string.IsNullOrEmpty(validSpineExePath))
                {
                    progress?.Report("传入路径无效，开始自动查找 Spine 可执行文件...");
                    validSpineExePath = FindSpineExecutableFromRegistry();

                    if (string.IsNullOrEmpty(validSpineExePath))
                    {
                        validSpineExePath = FindSpineExecutableFromCommonPaths();
                    }
                }

                // 4. 最终验证
                if (string.IsNullOrEmpty(validSpineExePath))
                {
                    progress?.Report("错误: 找不到 Spine 可执行文件");
                    return false;
                }

                progress?.Report($"最终使用的 Spine 路径: {validSpineExePath}");

                // 5. 验证其他参数
                if (spineFiles == null || spineFiles.Count == 0)
                {
                    progress?.Report("错误: 没有选择要转换的文件");
                    return false;
                }

                var successCount = 0;
                var totalCount = spineFiles.Count;

                progress?.Report($"开始批量转换 {totalCount} 个文件...");

                foreach (var spineFile in spineFiles)
                {
                    if (!File.Exists(spineFile))
                    {
                        progress?.Report($"跳过不存在的文件: {spineFile}");
                        continue;
                    }

                    progress?.Report($"正在处理: {Path.GetFileName(spineFile)} ({successCount + 1}/{totalCount})");

                    // 6. 调用转换方法，确保传递正确的路径
                    var success = await ExportSpineFileAsync(validSpineExePath, spineFile, settings, progress);
                    if (success)
                    {
                        successCount++;
                        progress?.Report($"✓ 成功转换: {Path.GetFileName(spineFile)}");
                    }
                    else
                    {
                        progress?.Report($"✗ 转换失败: {Path.GetFileName(spineFile)}");
                    }
                }

                progress?.Report($"批量转换完成！ 成功: {successCount}/{totalCount}");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                progress?.Report($"批量转换过程中发生错误: {ex.Message}");
                return false;
            }
        }

        // 新的注册表查找方法
        private string? FindSpineExecutableFromRegistry()
        {
            try
            {
                // 查找注册表中的 Spine 安装路径
                var registryPaths = new[]
                {
                    @"SOFTWARE\Esoteric Software\Spine",
                    @"SOFTWARE\WOW6432Node\Esoteric Software\Spine"
                };

                var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };

                foreach (var hive in hives)
                {
                    foreach (var regPath in registryPaths)
                    {
                        try
                        {
                            using var key = hive.OpenSubKey(regPath);

                            if (key != null)
                            {
                                // 尝试不同的值名称
                                var valueNames = new[] { "InstallPath", "Path", "InstallDir", "" };

                                foreach (var valueName in valueNames)
                                {
                                    var installPath = key.GetValue(valueName)?.ToString();
                                    if (!string.IsNullOrEmpty(installPath))
                                    {
                                        // 尝试不同的可执行文件名
                                        var exeNames = new[] { "Spine.exe", "Spine.com", "spine.exe", "spine.com" };

                                        foreach (var exeName in exeNames)
                                        {
                                            var fullPath = Path.Combine(installPath, exeName);
                                            if (File.Exists(fullPath))
                                            {
                                                return fullPath;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // 忽略单个注册表项的错误，继续尝试下一个
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // 忽略注册表访问错误
            }

            return null;
        }

        // 新的常见路径查找方法
        private string? FindSpineExecutableFromCommonPaths()
        {
            string?[] commonPaths =
            [
                @"C:\Program Files\Spine\Spine.exe",
                @"C:\Program Files\Spine\Spine.com",
                @"C:\Program Files (x86)\Spine\Spine.exe",
                @"C:\Program Files (x86)\Spine\Spine.com",
                @"C:\Spine\Spine.exe",
                @"C:\Spine\Spine.com",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spine",
                    "Spine.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spine",
                    "Spine.com")
            ];

            // 添加环境变量路径
            var spinePath = Environment.GetEnvironmentVariable("SPINE_PATH");
            if (!string.IsNullOrEmpty(spinePath))
            {
                string?[] envPaths =
                [
                    spinePath,
                    Path.Combine(spinePath, "Spine.exe"),
                    Path.Combine(spinePath, "Spine.com")
                ];

                foreach (var path in envPaths)
                {
                    if (File.Exists(path) && ValidateSpineExecutable(path))
                    {
                        return path;
                    }
                }
            }

            // 检查常见路径
            foreach (var path in commonPaths)
            {
                if (File.Exists(path) && ValidateSpineExecutable(path))
                {
                    return path;
                }
            }

            return null;
        }

        // 保留原有的 FindSpineExecutable 方法作为备用
        private string? FindSpineExecutable()
        {
            // 首先尝试注册表
            var regPath = FindSpineExecutableFromRegistry();
            if (!string.IsNullOrEmpty(regPath))
                return regPath;

            // 然后尝试常见路径
            return FindSpineExecutableFromCommonPaths();
        }

        private async Task<bool> ExportSpineFileAsync(string? spineExePath, string spineFile,
            ConversionSettings settings, IProgress<string>? progress)
        {
            string tempInputFile = null;
            string tempOutputDir = null;

            try
            {
                // 立即验证参数
                progress?.Report($"DEBUG: ExportSpineFileAsync 接收到的 spineExePath = '{spineExePath ?? "NULL"}'");

                if (string.IsNullOrEmpty(spineExePath))
                {
                    progress?.Report("致命错误: spineExePath 为空！");
                    return false;
                }

                // 验证 spineExePath
                if (!File.Exists(spineExePath))
                {
                    progress?.Report($"错误: Spine 可执行文件不存在: {spineExePath}");
                    return false;
                }

                // 验证输入文件
                if (!File.Exists(spineFile))
                {
                    progress?.Report($"错误: Spine 项目文件不存在: {spineFile}");
                    return false;
                }

                var outputDir = settings.OutputDirectory;
                var baseFileName = Path.GetFileNameWithoutExtension(spineFile);

                // 确保输出目录存在
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 处理中文路径问题
                var actualInputFile = spineFile;
                var actualOutputDir = outputDir;

                // 检查输入文件路径是否包含非ASCII字符（包括中文）
                if (ContainsNonAsciiCharacters(spineFile))
                {
                    var tempDir = Path.GetTempPath();
                    var extension = Path.GetExtension(spineFile);
                    tempInputFile = Path.Combine(tempDir,
                        $"spine_temp_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}");

                    // 复制原文件到临时位置
                    File.Copy(spineFile, tempInputFile);
                    actualInputFile = tempInputFile;
                    progress?.Report($"检测到中文路径，创建临时输入文件: {tempInputFile}");

                    // 同时复制相关的 .atlas 和图片文件
                    var sourceDir = Path.GetDirectoryName(spineFile);
                    var tempFileDir = Path.GetDirectoryName(tempInputFile);
                    var baseNameWithoutExt = Path.GetFileNameWithoutExtension(spineFile);
                    var tempBaseName = Path.GetFileNameWithoutExtension(tempInputFile);

                    // 复制 .atlas 文件
                    var atlasFile = Path.Combine(sourceDir, baseNameWithoutExt + ".atlas");
                    if (File.Exists(atlasFile))
                    {
                        var tempAtlasFile = Path.Combine(tempFileDir, tempBaseName + ".atlas");
                        File.Copy(atlasFile, tempAtlasFile);
                        _tempFiles.Add(tempAtlasFile);
                        progress?.Report($"复制相关文件: {atlasFile} -> {tempAtlasFile}");
                    }

                    // 复制图片文件
                    var imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
                    foreach (var imgExt in imageExtensions)
                    {
                        var imgFile = Path.Combine(sourceDir, baseNameWithoutExt + imgExt);
                        if (File.Exists(imgFile))
                        {
                            if (tempFileDir != null)
                            {
                                var tempImgFile = Path.Combine(tempFileDir, tempBaseName + imgExt);
                                File.Copy(imgFile, tempImgFile);
                                _tempFiles.Add(tempImgFile);
                                progress?.Report($"复制相关文件: {imgFile} -> {tempImgFile}");
                            }
                        }
                    }

                    _tempFiles.Add(tempInputFile);
                }

                // 检查输出目录是否包含非ASCII字符
                if (ContainsNonAsciiCharacters(outputDir))
                {
                    tempOutputDir = Path.Combine(Path.GetTempPath(), $"spine_output_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempOutputDir);
                    actualOutputDir = tempOutputDir;
                    progress?.Report($"检测到中文输出路径，创建临时输出目录: {tempOutputDir}");
                }

                // 构建导出参数
                var arguments = new List<string>
                {
                    "-i", $"\"{actualInputFile}\"",
                    "-o", $"\"{actualOutputDir}\""
                };

                // 添加导出设置
                if (!string.IsNullOrEmpty(settings.ExportSettingsPath) && File.Exists(settings.ExportSettingsPath))
                {
                    arguments.AddRange(["-e", $"\"{settings.ExportSettingsPath}\""]);
                    progress?.Report($"使用自定义导出设置: {settings.ExportSettingsPath}");
                }
                else
                {
                    var defaultSettingsPath = GetDefaultExportSettingsPath();
                    if (File.Exists(defaultSettingsPath))
                    {
                        arguments.AddRange(["-e", $"\"{defaultSettingsPath}\""]);
                        progress?.Report($"使用默认导出设置: {defaultSettingsPath}");
                    }
                    else
                    {
                        arguments.AddRange([
                            "--export", "json",
                            "--export", "atlas",
                            "--export", "images"
                        ]);
                        progress?.Report($"未找到默认导出设置文件: {defaultSettingsPath}，使用默认命令行参数");
                    }
                }

                // 执行 Spine 导出命令 - 关键修改：添加编码设置
                var processInfo = new ProcessStartInfo
                {
                    FileName = spineExePath,
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(actualInputFile), // 使用处理后的输入文件目录

                    // 关键：设置编码为 UTF-8
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                // 设置环境变量强制使用 UTF-8
                processInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                processInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
                processInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
                processInfo.EnvironmentVariables["CHCP"] = "65001"; // Windows UTF-8 代码页

                // 修改日志显示，确保显示完整命令
                progress?.Report($"执行命令: \"{processInfo.FileName}\" {processInfo.Arguments}");

                using (var process = new Process())
                {
                    process.StartInfo = processInfo;
                    var outputData = new List<string>();
                    var errorData = new List<string>();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputData.Add(e.Data);
                            progress?.Report($"输出: {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorData.Add(e.Data);
                            progress?.Report($"错误: {e.Data}");
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 添加超时处理
                    var timeoutMs = 300000; // 5分钟超时
                    var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

                    if (!completed)
                    {
                        progress?.Report("转换超时，正在终止进程...");
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // ignored
                        }

                        return false;
                    }

                    if (process.ExitCode == 0)
                    {
                        progress?.Report($"导出成功完成: {baseFileName}");

                        // 如果使用了临时输出目录，复制文件回原目录
                        if (tempOutputDir != null)
                        {
                            progress?.Report("正在复制文件到最终输出目录...");
                            CopyDirectory(tempOutputDir, outputDir, true);
                            progress?.Report("已将文件复制回原输出目录");
                        }

                        // 验证输出文件
                        var expectedFiles = new[] { ".json", ".atlas", ".png" };
                        var hasOutput = false;

                        foreach (var ext in expectedFiles)
                        {
                            var outputFile = Path.Combine(outputDir, baseFileName + ext);
                            if (File.Exists(outputFile))
                            {
                                hasOutput = true;
                                progress?.Report($"✓ 生成文件: {outputFile}");
                            }
                        }

                        if (!hasOutput)
                        {
                            progress?.Report("⚠ 警告: 未找到预期的输出文件，但命令执行成功");
                        }

                        if (settings.ResetImagePaths || settings.ResetAudioPaths)
                        {
                            await ProcessExportedJsonAsync(outputDir, baseFileName, settings, progress);
                        }

                        return true;
                    }
                    else
                    {
                        progress?.Report($"导出失败，退出代码: {process.ExitCode}");
                        if (errorData.Count > 0)
                        {
                            progress?.Report($"错误详情: {string.Join(Environment.NewLine, errorData)}");
                        }

                        if (outputData.Count > 0)
                        {
                            progress?.Report($"输出详情: {string.Join(Environment.NewLine, outputData)}");
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"导出异常: {ex.Message}");
                progress?.Report($"异常堆栈: {ex.StackTrace}");
                return false;
            }
            finally
            {
                // 清理临时文件
                if (tempInputFile != null && File.Exists(tempInputFile))
                {
                    try
                    {
                        File.Delete(tempInputFile);
                        progress?.Report("已清理临时输入文件");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"清理临时输入文件失败: {ex.Message}");
                    }
                }

                if (tempOutputDir != null && Directory.Exists(tempOutputDir))
                {
                    try
                    {
                        Directory.Delete(tempOutputDir, true);
                        progress?.Report("已清理临时输出目录");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"清理临时输出目录失败: {ex.Message}");
                    }
                }
            }
        }

        private static bool ContainsNonAsciiCharacters(string path)
        {
            return path.Any(c => c > 127);
        }

        private void CopyDirectory(string sourceDir, string targetDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                return;

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(targetDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(targetDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newTargetDir = Path.Combine(targetDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newTargetDir, true);
                }
            }
        }

        private Encoding DetectFileEncoding(string filePath)
        {
            try
            {
                // 读取文件的前几个字节来检测BOM
                byte[] bom = new byte[4];
                using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    file.ReadExactly(bom, 0, 4);
                }

                // 检查UTF-8 BOM (EF BB BF)
                if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return Encoding.UTF8;

                // 检查UTF-16 LE BOM (FF FE)
                if (bom[0] == 0xFF && bom[1] == 0xFE)
                    return Encoding.Unicode;

                // 检查UTF-16 BE BOM (FE FF)
                if (bom[0] == 0xFE && bom[1] == 0xFF)
                    return Encoding.BigEndianUnicode;

                // 检查UTF-32 LE BOM (FF FE 00 00)
                if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                    return Encoding.UTF32;

                // 默认返回UTF-8 without BOM
                return new UTF8Encoding(false);
            }
            catch
            {
                return new UTF8Encoding(false);
            }
        }


        private async Task ProcessExportedJsonAsync(string outputDir, string baseFileName,
            ConversionSettings settings, IProgress<string>? progress)
        {
            try
            {
                progress?.Report("=== 开始 JSON 后处理 ===");
                progress?.Report($"输出目录: {outputDir}");
                progress?.Report($"基础文件名: {baseFileName}");
                progress?.Report($"重置图片路径: {settings.ResetImagePaths}");
                progress?.Report($"重置音频路径: {settings.ResetAudioPaths}");

                // 检查是否需要处理
                if (settings is { ResetImagePaths: false, ResetAudioPaths: false })
                {
                    progress?.Report("跳过 JSON 处理 - 未启用路径重置选项");
                    return;
                }

                // 查找导出的 JSON 文件
                var jsonFile = Path.Combine(outputDir, $"{baseFileName}.json");
                progress?.Report($"查找 JSON 文件: {jsonFile}");

                if (!File.Exists(jsonFile))
                {
                    progress?.Report($"未找到 JSON 文件: {jsonFile}");

                    // 列出目录中的所有文件进行调试
                    if (Directory.Exists(outputDir))
                    {
                        var files = Directory.GetFiles(outputDir);
                        progress?.Report($"目录中的文件: {string.Join(", ", files.Select(Path.GetFileName))}");
                    }

                    return;
                }

                progress?.Report($"✓ 找到 JSON 文件，大小: {new FileInfo(jsonFile).Length} 字节");

                // *** 关键修改：检测文件编码 ***
                var originalEncoding = DetectFileEncoding(jsonFile);
                progress?.Report($"检测到文件编码: {originalEncoding.EncodingName}");

                // *** 关键修改：使用检测到的编码读取文件 ***
                string jsonContent = await File.ReadAllTextAsync(jsonFile, originalEncoding);
                progress?.Report($"JSON 内容长度: {jsonContent.Length} 字符");

                // 用正则表达式安全地提取路径信息进行调试
                var imagesMatch = Regex.Match(jsonContent, @"""images""\s*:\s*""([^""]*)""");
                if (imagesMatch.Success)
                    progress?.Report($"原始 images 路径: {imagesMatch.Groups[1].Value}");

                var audioMatch = Regex.Match(jsonContent, @"""audio""\s*:\s*""([^""]*)""");
                if (audioMatch.Success)
                    progress?.Report($"原始 audio 路径: {audioMatch.Groups[1].Value}");

                // 修改 JSON
                progress?.Report("开始修改 JSON...");
                var modifiedJson = ModifySpineJson(jsonContent, settings, progress);
                progress?.Report($"ModifySpineJson 返回结果长度: {modifiedJson?.Length ?? 0}");

                if (!string.IsNullOrEmpty(modifiedJson))
                {
                    // 备份原文件
                    var backupFile = jsonFile + ".backup";
                    File.Copy(jsonFile, backupFile, true);
                    progress?.Report($"✓ 已备份原文件: {Path.GetFileName(backupFile)}");

                    // *** 关键修改：使用原始编码写回文件 ***
                    await File.WriteAllTextAsync(jsonFile, modifiedJson, originalEncoding);
                    progress?.Report($"✓ JSON 修改完成，已保持原始编码格式 ({originalEncoding.EncodingName})");
                    progress?.Report($"新文件大小: {modifiedJson.Length} 字符");
                }
                else
                {
                    progress?.Report("JSON 修改失败 - 返回空内容");
                }

                progress?.Report("=== JSON 后处理完成 ===");
            }
            catch (Exception ex)
            {
                progress?.Report($"JSON 处理错误: {ex.Message}");
                progress?.Report($"错误详情: {ex.StackTrace}");
            }
        }


        private string ModifySpineJson(string jsonContent, ConversionSettings settings, IProgress<string>? progress,
            string originalFilePath = null)
        {
            try
            {
                string result = jsonContent;

                if (settings.ResetImagePaths)
                {
                    var matches = Regex.Matches(result, @"""images""\s*:\s*""[^""]*""");
                    progress?.Report($"找到 {matches.Count} 个 images 匹配项");
                    foreach (Match match in matches)
                    {
                        progress?.Report($"匹配内容: {match.Value}");
                    }

                    result = Regex.Replace(result, @"""images""\s*:\s*""[^""]*""", @"""images"": ""./images/""");
                    progress?.Report("✓ 已重置 images 路径为 './images/'");
                }

                if (settings.ResetAudioPaths)
                {
                    var matches = Regex.Matches(result, @"""audio""\s*:\s*""[^""]*""");
                    progress?.Report($"找到 {matches.Count} 个 audio 匹配项");
                    foreach (Match match in matches)
                    {
                        progress?.Report($"匹配内容: {match.Value}");
                    }

                    result = Regex.Replace(result, @"""audio""\s*:\s*""[^""]*""", @"""audio"": """"");
                    progress?.Report("✓ 已重置 audio 路径为空字符串");
                }

                return result;
            }
            catch (Exception ex)
            {
                progress?.Report($"JSON 修改失败: {ex.Message}");
                return jsonContent;
            }
        }

        // 在保存文件时使用 UTF-8 with BOM
        public void SaveModifiedJson(string filePath, string content)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        private string GetDefaultExportSettingsPath()
        {
            // 获取当前可执行文件的目录，而不是应用程序域的基目录
            var executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            // 构建 config 文件夹下的默认设置文件路径
            var configPath = Path.Combine(executableDir, "config", "DefaultExportSettings.json");

            return configPath;
        }


        // 实现 ConvertAsync 方法 - 这是接口要求的核心方法
        public async Task<bool> ConvertAsync(SpineAsset asset, ConversionSettings settings,
            IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"开始转换: {Path.GetFileName(asset.FilePath)}");

                // 验证输入参数
                if (!ValidateInputs(asset, settings, progress))
                    return false;

                // 获取有效的 Spine 可执行文件路径
                var spineExePath = GetValidSpineExecutablePath(settings.SpineExecutablePath, progress);
                if (string.IsNullOrEmpty(spineExePath))
                {
                    progress?.Report("错误: 找不到有效的 Spine 可执行文件");
                    return false;
                }

                // 构建命令行参数（会创建临时导出设置文件）
                var arguments = BuildCommandLineArguments(asset, settings);
                progress?.Report($"执行命令: {spineExePath} {arguments}");

                // 执行 Spine 转换
                var success = await ExecuteSpineConversion(spineExePath, arguments, progress);

                if (success)
                {
                    progress?.Report("导出成功完成");

                    // 添加 JSON 后处理
                    if (settings.ResetImagePaths || settings.ResetAudioPaths)
                    {
                        progress?.Report("开始 JSON 后处理...");
                        var baseFileName = Path.GetFileNameWithoutExtension(asset.FilePath);
                        await ProcessExportedJsonAsync(settings.OutputDirectory, baseFileName, settings, progress);
                        progress?.Report("JSON 后处理完成");
                    }

                    progress?.Report("转换完成！");
                    return true;
                }
                else
                {
                    progress?.Report("转换失败！");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"转换过程中发生错误: {ex.Message}");
                return false;
            }
            finally
            {
                // 清理临时文件
                CleanupTempFiles();
            }
        }


        private string? GetValidSpineExecutablePath(string providedPath, IProgress<string>? progress)
        {
            // 1. 如果提供了路径，先验证
            if (!string.IsNullOrEmpty(providedPath))
            {
                var cleanPath = providedPath.Trim('"');
                if (File.Exists(cleanPath) && ValidateSpineExecutable(cleanPath))
                {
                    progress?.Report($"使用提供的 Spine 路径: {cleanPath}");
                    return cleanPath;
                }
            }

            // 2. 自动查找
            progress?.Report("自动查找 Spine 可执行文件...");
            var foundPath = FindSpineExecutable();

            if (!string.IsNullOrEmpty(foundPath))
            {
                progress?.Report($"找到 Spine 可执行文件: {foundPath}");
                return foundPath;
            }

            return null;
        }

        private void CleanupTempFiles()
        {
            foreach (var tempFile in _tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // 忽略删除失败
                }
            }

            _tempFiles.Clear();
        }

        private bool ValidateInputs(SpineAsset asset, ConversionSettings settings, IProgress<string>? progress)
        {
            // 检查源文件
            if (string.IsNullOrEmpty(asset.FilePath) || !File.Exists(asset.FilePath))
            {
                progress?.Report("错误: 找不到源文件");
                return false;
            }

            // 检查输出目录
            if (string.IsNullOrEmpty(settings.OutputDirectory))
            {
                progress?.Report("错误: 输出目录不能为空");
                return false;
            }

            // 创建输出目录（如果不存在）
            try
            {
                Directory.CreateDirectory(settings.OutputDirectory);
            }
            catch (Exception ex)
            {
                progress?.Report($"错误: 无法创建输出目录 - {ex.Message}");
                return false;
            }

            return true;
        }

        private string BuildCommandLineArguments(SpineAsset asset, ConversionSettings settings)
        {
            var arguments = new List<string>();

            // 基本参数
            arguments.AddRange(["-i", $"\"{asset.FilePath}\""]);
            arguments.AddRange(["-o", $"\"{settings.OutputDirectory}\""]);

            // 创建临时导出设置文件
            var tempSettingsPath = CreateTempExportSettings(asset, settings);
            _tempFiles.Add(tempSettingsPath);
            arguments.AddRange(["-e", $"\"{tempSettingsPath}\""]);

            return string.Join(" ", arguments);
        }

        private string CreateBasicExportSettings(SpineAsset asset, ConversionSettings settings)
        {
            var exportSettings = new
            {
                @class = "export-json",
                extension = ".json",
                format = "JSON",
                prettyPrint = false,
                nonessential = true,
                cleanUp = true,
                packAtlas = new
                {
                    stripWhitespaceX = true,
                    stripWhitespaceY = true,
                    rotation = true,
                    alias = true,
                    ignoreBlankImages = false,
                    alphaThreshold = 3,
                    minWidth = 16,
                    minHeight = 16,
                    maxWidth = 2048,
                    maxHeight = 2048,
                    pot = false,
                    multipleOfFour = false,
                    square = false,
                    outputFormat = "png",
                    jpegQuality = 0.9,
                    premultiplyAlpha = false,
                    bleed = true,
                    scale = new[] { 1 },
                    scaleSuffix = new[] { "" },
                    scaleResampling = new[] { "bicubic" },
                    paddingX = 2,
                    paddingY = 2,
                    edgePadding = true,
                    duplicatePadding = false,
                    filterMin = "Linear",
                    filterMag = "Linear",
                    wrapX = "ClampToEdge",
                    wrapY = "ClampToEdge",
                    format = "RGBA8888",
                    atlasExtension = ".atlas",
                    combineSubdirectories = false,
                    flattenPaths = false,
                    useIndexes = false,
                    debug = false,
                    fast = false,
                    limitMemory = true,
                    currentProject = true,
                    packing = "rectangles",
                    prettyPrint = false,
                    legacyOutput = false,
                    webp = (object)null,
                    bleedIterations = 2,
                    ignore = false,
                    separator = "_",
                    silent = false
                },
                packSource = "attachments",
                packTarget = "perskeleton",
                warnings = true,
                version = (object)null,
                output = settings.OutputDirectory,
                forceAll = false,
                input = asset.FilePath,
                open = false
            };

            var tempPath = Path.GetTempFileName();
            var tempJsonPath = Path.ChangeExtension(tempPath, ".json");
            File.Delete(tempPath);

            // 使用自定义序列化选项处理特殊字段
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(exportSettings, options);
            File.WriteAllText(tempJsonPath, json);

            _tempFiles.Add(tempJsonPath);
            return tempJsonPath;
        }

        private object ConvertPackAtlasElement(JsonElement element, ConversionSettings settings)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return ConvertJsonElement(element);
            }

            var dict = new Dictionary<string, object>();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name == "maxWidth" && settings.MaxWidth > 0)
                {
                    dict[property.Name] = settings.MaxWidth;
                }
                else if (property.Name == "maxHeight" && settings.MaxHeight > 0)
                {
                    dict[property.Name] = settings.MaxHeight;
                }
                else
                {
                    dict[property.Name] = ConvertJsonElement(property.Value);
                }
            }

            // 如果原始配置中没有 maxWidth/maxHeight，但设置中有指定，则添加
            if (!dict.ContainsKey("maxWidth") && settings.MaxWidth > 0)
            {
                dict["maxWidth"] = settings.MaxWidth;
            }

            if (!dict.ContainsKey("maxHeight") && settings.MaxHeight > 0)
            {
                dict["maxHeight"] = settings.MaxHeight;
            }

            return dict;
        }


        private string CreateTempExportSettings(SpineAsset asset, ConversionSettings settings)
        {
            var sourceSettingsPath = !string.IsNullOrEmpty(settings.ExportSettingsPath) &&
                                     File.Exists(settings.ExportSettingsPath)
                ? settings.ExportSettingsPath
                : GetDefaultExportSettingsPath();

            // 如果默认配置文件不存在，创建一个基本的配置
            if (!File.Exists(sourceSettingsPath))
            {
                return CreateBasicExportSettings(asset, settings);
            }

            try
            {
                // 读取原始设置
                var jsonContent = File.ReadAllText(sourceSettingsPath);

                // 使用 JsonDocument 来保持原始结构
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // 创建新的设置字典
                var newSettings = new Dictionary<string, object>();

                // 复制所有原始设置，保持原始类型
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "input")
                    {
                        newSettings[property.Name] = asset.FilePath;
                    }
                    else if (property.Name == "output")
                    {
                        newSettings[property.Name] = settings.OutputDirectory;
                    }
                    else if (property.Name == "packAtlas")
                    {
                        // 特殊处理 packAtlas 配置，修改图集尺寸
                        newSettings[property.Name] = ConvertPackAtlasElement(property.Value, settings);
                    }
                    else
                    {
                        // 保持原始 JSON 元素的结构
                        newSettings[property.Name] = ConvertJsonElement(property.Value);
                    }
                }

                // 确保必要的字段存在
                if (!newSettings.ContainsKey("input"))
                    newSettings["input"] = asset.FilePath;
                if (!newSettings.ContainsKey("output"))
                    newSettings["output"] = settings.OutputDirectory;

                // 创建临时文件
                var tempPath = Path.GetTempFileName();
                var tempJsonPath = Path.ChangeExtension(tempPath, ".json");
                File.Delete(tempPath);

                // 写入新设置，保持格式
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };

                var newJsonContent = JsonSerializer.Serialize(newSettings, options);
                File.WriteAllText(tempJsonPath, newJsonContent);

                _tempFiles.Add(tempJsonPath);
                return tempJsonPath;
            }
            catch (Exception ex)
            {
                // 如果解析失败，创建基本配置
                return CreateBasicExportSettings(asset, settings);
            }
        }

        private object ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElement(property.Value);
                    }

                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElement(item));
                    }

                    return list.ToArray();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return element.GetDecimal();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.GetRawText();
            }
        }

        private async Task<bool> ExecuteSpineConversion(string? spineExePath, string arguments,
            IProgress<string>? progress)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = spineExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(spineExePath)
            };

            using var process = new Process();
            process.StartInfo = processInfo;

            var outputData = new List<string>();
            var errorData = new List<string>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputData.Add(e.Data);
                    progress?.Report($"输出: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorData.Add(e.Data);
                    progress?.Report($"错误: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode == 0)
            {
                progress?.Report("导出成功完成");
                return true;
            }
            else
            {
                progress?.Report($"导出失败，退出代码: {process.ExitCode}");
                if (errorData.Count > 0)
                {
                    progress?.Report($"错误详情: {string.Join(Environment.NewLine, errorData)}");
                }

                return false;
            }
        }

        // 验证是否为有效的 Spine 可执行文件
        public bool ValidateSpineExecutable(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var cleanPath = path.Trim('"');

            if (!File.Exists(cleanPath))
                return false;

            var fileName = Path.GetFileName(cleanPath).ToLower();
            return fileName == "spine.exe" || fileName == "spine.com" || fileName == "spine";
        }

        public string GetSpineVersion(string? executablePath)
        {
            try
            {
                if (!ValidateSpineExecutable(executablePath))
                    return "未知版本";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // 解析版本信息
                    return ParseVersionFromOutput(output);
                }
            }
            catch (Exception)
            {
                // 忽略错误，返回默认值
            }

            return "未知版本";
        }

        private string ParseVersionFromOutput(string output)
        {
            // 这里需要根据实际的 Spine 命令行输出格式来解析版本号
            // 示例实现，您可能需要根据实际情况调整
            if (string.IsNullOrEmpty(output))
                return "未知版本";

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("version", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Trim();
                }
            }

            return output.Split('\n')[0].Trim();
        }

        // 添加扫描目录方法的实现
    }
}