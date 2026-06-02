# PlugHub Framework Auto Update 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 在「关于」页签增加 `检查更新` 和 `更新框架` 两个按钮，下载 latest release 包，并在 Revit 关闭后静默只覆盖框架 DLL。

**架构：** `PlugHub.Framework.Updates` 负责 latest release 查询、版本比较、下载和 zip 校验；`PlugHub.Revit2020` 只负责按钮和底部状态提示；`PlugHub.Updater` 是静默外部 EXE，等待 Revit 进程退出后复制白名单 DLL，不覆盖 addin、packages、config、缓存和日志。

**技术栈：** C#、.NET Framework 4.8、WPF、WinExe、`HttpWebRequest`、`ZipArchive`、`JavaScriptSerializer`、PowerShell release workflow、`PlugHub.StaticValidation`。

---

## 文件结构

- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdateService.cs`  
  编排检查更新、下载更新包、校验 zip、启动静默 updater。
- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdateModels.cs`  
  保存 release 元数据、检查结果、下载结果和操作结果模型。
- 创建：`src/PlugHub.Framework/Updates/ReleaseClient.cs`  
  获取 GitHub latest release JSON，并解析出 tag 和资产列表。
- 创建：`src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs`  
  下载 HTTPS release 资产到 `%LocalAppData%\PlugHub\updates\<tag>\`。
- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs`  
  校验 zip 头、路径安全和根目录白名单 DLL。
- 创建：`src/PlugHub.Updater/PlugHub.Updater.csproj`  
  net48 静默 WinExe 项目。
- 创建：`src/PlugHub.Updater/Program.cs`  
  updater 入口，解析参数、执行更新、日志兜底。
- 创建：`src/PlugHub.Updater/UpdaterArguments.cs`  
  解析 `/payloadZip`、`/installDir`、`/targetVersion`、`/revitProcessId` 参数，同时支持 Base64 变体。
- 创建：`src/PlugHub.Updater/FrameworkDllUpdater.cs`  
  等待 Revit 退出，备份并复制 zip 中根目录 DLL。
- 创建：`src/PlugHub.Updater/UpdaterLogger.cs`  
  写入安装目录 `logs`，失败时回退到 `%LocalAppData%\PlugHub\logs`。
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`  
  在 `BuildAboutTab()` 中加入两个按钮和异步事件处理。
- 修改：`src/PlugHub.StaticValidation/Program.cs`  
  增加静态规则，锁定两个按钮、服务边界、updater 项目、DLL-only 更新和 release 打包。
- 修改：`PlugHub.sln`、`PlugHub.slnx`  
  加入 `PlugHub.Updater` 项目。
- 修改：`scripts/build-revit2020.ps1`  
  本地 staging 输出中包含 `PlugHub.Updater.exe`。
- 修改：`.github/workflows/release.yml`、`.gitee/workflows/release.yml`  
  release zip 打包前构建并复制 `PlugHub.Updater.exe`。
- 修改：`README.md`、`docs/development.md`  
  记录关于页两个按钮、重启后静默覆盖 DLL、非 Revit 验证边界。

---

### 任务 1：静态验证先锁定规格边界

**文件：**
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：在验证入口中加入新规则调用**

在 `ValidateUninstallerPackaging();` 后面加入：

```csharp
ValidateFrameworkAutoUpdateSpecification();
```

- [ ] **步骤 2：扩展必需文件列表**

在 `ValidateRequiredFiles()` 的 `required` 数组中加入：

```csharp
"src/PlugHub.Framework/Updates/FrameworkUpdateService.cs",
"src/PlugHub.Framework/Updates/FrameworkUpdateModels.cs",
"src/PlugHub.Framework/Updates/ReleaseClient.cs",
"src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs",
"src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs",
"src/PlugHub.Updater/PlugHub.Updater.csproj",
"src/PlugHub.Updater/Program.cs",
"src/PlugHub.Updater/UpdaterArguments.cs",
"src/PlugHub.Updater/FrameworkDllUpdater.cs",
"src/PlugHub.Updater/UpdaterLogger.cs",
```

- [ ] **步骤 3：新增验证方法**

在 `ValidateUninstallerPackaging()` 附近新增：

```csharp
private static void ValidateFrameworkAutoUpdateSpecification()
{
    var solution = ReadText("PlugHub.sln");
    var solutionX = ReadText("PlugHub.slnx");
    var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
    var frameworkProject = ReadText("src/PlugHub.Framework/PlugHub.Framework.csproj");
    var service = ReadText("src/PlugHub.Framework/Updates/FrameworkUpdateService.cs");
    var releaseClient = ReadText("src/PlugHub.Framework/Updates/ReleaseClient.cs");
    var validator = ReadText("src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs");
    var updaterProject = ReadText("src/PlugHub.Updater/PlugHub.Updater.csproj");
    var updaterProgram = ReadText("src/PlugHub.Updater/Program.cs");
    var updaterArguments = ReadText("src/PlugHub.Updater/UpdaterArguments.cs");
    var updaterRunner = ReadText("src/PlugHub.Updater/FrameworkDllUpdater.cs");
    var githubWorkflow = ReadText(".github/workflows/release.yml");
    var giteeWorkflow = ReadText(".gitee/workflows/release.yml");
    var buildScript = ReadText("scripts/build-revit2020.ps1");
    var readme = ReadText("README.md");
    var development = ReadText("docs/development.md");

    Require(frameworkProject.Contains("System.Web.Extensions"), "framework update release JSON parsing must keep using available net48 framework references.");
    Require(solution.Contains("src\\PlugHub.Updater\\PlugHub.Updater.csproj"), "updater project must be included in PlugHub.sln.");
    Require(solutionX.Contains("src/PlugHub.Updater/PlugHub.Updater.csproj"), "updater project must be included in PlugHub.slnx.");
    Require(updaterProject.Contains("<TargetFramework>net48</TargetFramework>") && updaterProject.Contains("<OutputType>WinExe</OutputType>"), "updater must build as a silent net48 Windows EXE.");
    Require(settingsWindow.Contains("\"检查更新\"") && settingsWindow.Contains("\"更新框架\""), "About tab must expose Check Update and Update Framework buttons.");
    Require(settingsWindow.Contains("CheckFrameworkUpdate") && settingsWindow.Contains("UpdateFramework") && settingsWindow.Contains("RefreshStatus"), "About tab update actions must write to the bottom-left status text.");
    Require(service.Contains("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest") && service.Contains("PlugHub-Revit2020-"), "framework update service must target GitHub latest release assets.");
    Require(service.Contains("PlugHub.Updater.exe") && service.Contains("StartUpdater"), "framework update service must start the silent updater instead of copying DLLs in-process.");
    Require(releaseClient.Contains("HttpWebRequest") && releaseClient.Contains("UserAgent") && releaseClient.Contains("JavaScriptSerializer"), "release client must use net48-compatible HTTP and JSON APIs.");
    Require(validator.Contains("PlugHub.Revit2020.dll") && validator.Contains("PlugHub.Framework.dll") && validator.Contains("PlugHub.Contracts.dll"), "update package validator must require the core framework DLLs.");
    Require(validator.Contains("IsSafeZipEntry") && validator.Contains("Path.DirectorySeparatorChar"), "update package validator must reject unsafe zip paths.");
    Require(updaterArguments.Contains("/payloadZip") && updaterArguments.Contains("/installDir") && updaterArguments.Contains("/targetVersion") && updaterArguments.Contains("/revitProcessId"), "updater must accept the documented arguments.");
    Require(updaterProgram.Contains("FrameworkDllUpdater") && !updaterProgram.Contains("Application.Run"), "updater must run silently without a WinForms window.");
    Require(updaterRunner.Contains("WaitForExit") && updaterRunner.Contains("CopyFrameworkDllsOnly"), "updater must wait for Revit exit before copying DLLs.");
    Require(updaterRunner.Contains("PlugHub.addin") && updaterRunner.Contains("SkipNonDllEntry") && !updaterRunner.Contains("Directory.Delete(installDirectory"), "updater must avoid addin/config/packages replacement and install directory deletion.");
    Require(githubWorkflow.Contains("Build PlugHub updater") && githubWorkflow.Contains("PlugHub.Updater.csproj"), "GitHub release workflow must build the updater before packaging the release zip.");
    Require(giteeWorkflow.Contains("Build PlugHub updater") && giteeWorkflow.Contains("PlugHub.Updater.csproj"), "Gitee release workflow must build the updater before packaging the release zip.");
    Require(buildScript.Contains("PlugHub.Updater.csproj") && buildScript.Contains("PlugHub.Updater.exe"), "local Revit 2020 build script must stage PlugHub.Updater.exe.");
    Require(readme.Contains("检查更新") && readme.Contains("更新框架") && readme.Contains("只覆盖框架 DLL"), "README must document framework auto-update behavior.");
    Require(development.Contains("检查更新") && development.Contains("更新框架") && development.Contains("不能声明 Revit 实机测试成功"), "development docs must document updater verification boundaries.");
}
```

- [ ] **步骤 4：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：FAIL，错误包含缺少 `src/PlugHub.Framework/Updates/FrameworkUpdateService.cs` 或 `src/PlugHub.Updater/PlugHub.Updater.csproj`。

---

### 任务 2：实现框架更新服务

**文件：**
- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdateModels.cs`
- 创建：`src/PlugHub.Framework/Updates/ReleaseClient.cs`
- 创建：`src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs`
- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs`
- 创建：`src/PlugHub.Framework/Updates/FrameworkUpdateService.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：创建更新模型**

创建 `FrameworkUpdateModels.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseAssetInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public sealed class ReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;
        public List<ReleaseAssetInfo> Assets { get; set; } = new List<ReleaseAssetInfo>();
    }

    public sealed class FrameworkUpdateCheckResult
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string AssetDownloadUrl { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FrameworkUpdateDownloadResult
    {
        public bool Success { get; set; }
        public string PackagePath { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FrameworkUpdateOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
```

- [ ] **步骤 2：实现 release client**

创建 `ReleaseClient.cs`，使用 `HttpWebRequest` 和 `JavaScriptSerializer`，保留 `ParseReleaseJson` 供静态验证或本地夹具调用：

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseClient
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";

        public ReleaseInfo GetLatest(Uri releaseUri)
        {
            if (releaseUri == null) throw new ArgumentNullException(nameof(releaseUri));
            if (!string.Equals(releaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release API must use HTTPS.");
            }

            var request = (HttpWebRequest)WebRequest.Create(releaseUri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream ?? Stream.Null))
            {
                return ParseReleaseJson(reader.ReadToEnd());
            }
        }

        public ReleaseInfo ParseReleaseJson(string json)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json ?? string.Empty) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("Release response is not a JSON object.");

            var release = new ReleaseInfo { TagName = StringValue(root, "tag_name") };
            if (root.TryGetValue("assets", out var assetsValue) && assetsValue is ArrayList assets)
            {
                foreach (var item in assets)
                {
                    if (!(item is Dictionary<string, object> asset)) continue;
                    release.Assets.Add(new ReleaseAssetInfo
                    {
                        Name = StringValue(asset, "name"),
                        DownloadUrl = StringValue(asset, "browser_download_url")
                    });
                }
            }

            return release;
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? SensitiveTextRedactor.Redact(Convert.ToString(value) ?? string.Empty)
                : string.Empty;
        }
    }
}
```

- [ ] **步骤 3：实现下载器**

创建 `ReleaseAssetDownloader.cs`，确保只接收 HTTPS：

```csharp
using System;
using System.IO;
using System.Net;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseAssetDownloader
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";

        public string Download(string downloadUrl, string targetDirectory, string fileName)
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release asset URL must be HTTPS.");
            }

            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, SafeFileName(fileName));

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var target = File.Create(targetPath))
            {
                if (stream == null) throw new InvalidDataException("Release asset response did not contain a body.");
                stream.CopyTo(target);
            }

            return targetPath;
        }

        private static string SafeFileName(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "PlugHub-update.zip" : value;
        }
    }
}
```

- [ ] **步骤 4：实现 zip 校验器**

创建 `FrameworkUpdatePackageValidator.cs`，只要求核心 DLL，不要求 `PlugHub.addin`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdatePackageValidator
    {
        private static readonly string[] RequiredDlls =
        {
            "PlugHub.Revit2020.dll",
            "PlugHub.Framework.dll",
            "PlugHub.Contracts.dll"
        };

        public void Validate(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("Framework update package was not found.", zipPath);
            }

            ValidateZipHeader(zipPath);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var rootDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    if (!IsSafeZipEntry(entry.FullName))
                    {
                        throw new InvalidDataException("Update package contains an unsafe path: " + entry.FullName);
                    }

                    if (IsRootDllEntry(entry.FullName))
                    {
                        rootDlls.Add(Path.GetFileName(entry.FullName));
                    }
                }

                foreach (var dll in RequiredDlls)
                {
                    if (!rootDlls.Contains(dll))
                    {
                        throw new InvalidDataException("Update package is missing framework DLL: " + dll);
                    }
                }
            }
        }

        private static void ValidateZipHeader(string zipPath)
        {
            var header = new byte[4];
            using (var source = File.OpenRead(zipPath))
            {
                if (source.Read(header, 0, header.Length) != header.Length)
                {
                    throw new InvalidDataException("Update package is not a valid zip file.");
                }
            }

            if (header[0] != 0x50 || header[1] != 0x4B)
            {
                throw new InvalidDataException("Update package is not a zip file.");
            }
        }

        public static bool IsSafeZipEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return !string.IsNullOrWhiteSpace(normalized)
                && !Path.IsPathRooted(normalized)
                && normalized.Split(Path.DirectorySeparatorChar).All(part => part != "..");
        }

        public static bool IsRootDllEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(normalized), normalized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **步骤 5：实现服务编排**

创建 `FrameworkUpdateService.cs`：

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdateService
    {
        private static readonly Uri DefaultLatestReleaseUri =
            new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest");

        private readonly ReleaseClient _releaseClient;
        private readonly ReleaseAssetDownloader _downloader;
        private readonly FrameworkUpdatePackageValidator _validator;

        public FrameworkUpdateService()
            : this(new ReleaseClient(), new ReleaseAssetDownloader(), new FrameworkUpdatePackageValidator())
        {
        }

        public FrameworkUpdateService(
            ReleaseClient releaseClient,
            ReleaseAssetDownloader downloader,
            FrameworkUpdatePackageValidator validator)
        {
            _releaseClient = releaseClient ?? throw new ArgumentNullException(nameof(releaseClient));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public FrameworkUpdateCheckResult Check(string currentVersion)
        {
            var release = _releaseClient.GetLatest(DefaultLatestReleaseUri);
            var assetName = "PlugHub-Revit2020-" + release.TagName + ".zip";
            var asset = release.Assets.FirstOrDefault(item =>
                string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));
            if (asset == null)
            {
                return FailedCheck(currentVersion, release.TagName, "latest release 没有 Revit 2020 更新包。");
            }

            var hasUpdate = IsNewer(release.TagName, currentVersion);
            return new FrameworkUpdateCheckResult
            {
                Success = true,
                HasUpdate = hasUpdate,
                CurrentVersion = currentVersion ?? string.Empty,
                LatestVersion = release.TagName,
                AssetName = asset.Name,
                AssetDownloadUrl = asset.DownloadUrl,
                Message = hasUpdate ? "发现新版本 " + release.TagName + "。" : "当前已是最新版本。"
            };
        }

        public FrameworkUpdateDownloadResult Download(FrameworkUpdateCheckResult checkResult)
        {
            if (checkResult == null || !checkResult.Success || !checkResult.HasUpdate)
            {
                return new FrameworkUpdateDownloadResult { Success = false, Message = "没有可下载的框架更新。" };
            }

            var targetDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlugHub",
                "updates",
                SafePathSegment(checkResult.LatestVersion));
            var packagePath = _downloader.Download(checkResult.AssetDownloadUrl, targetDirectory, checkResult.AssetName);
            _validator.Validate(packagePath);
            return new FrameworkUpdateDownloadResult
            {
                Success = true,
                PackagePath = packagePath,
                LatestVersion = checkResult.LatestVersion,
                Message = "框架更新已下载。"
            };
        }

        public FrameworkUpdateOperationResult StartUpdater(string baseDirectory, string packagePath, string targetVersion, int revitProcessId)
        {
            var updaterPath = Path.Combine(baseDirectory, "PlugHub.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                return new FrameworkUpdateOperationResult { Success = false, Message = "未找到框架更新器: " + updaterPath };
            }

            var arguments =
                Quote("/payloadZip") + " " + Quote(packagePath) + " " +
                Quote("/installDir") + " " + Quote(baseDirectory) + " " +
                Quote("/targetVersion") + " " + Quote(targetVersion) + " " +
                Quote("/revitProcessId") + " " + revitProcessId.ToString();
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return new FrameworkUpdateOperationResult { Success = true, Message = "框架更新已准备好，请重启 Revit。" };
        }

        private static FrameworkUpdateCheckResult FailedCheck(string currentVersion, string latestVersion, string message)
        {
            return new FrameworkUpdateCheckResult
            {
                Success = false,
                CurrentVersion = currentVersion ?? string.Empty,
                LatestVersion = latestVersion ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private static bool IsNewer(string remoteTag, string localVersion)
        {
            return Version.TryParse(TrimVersion(remoteTag), out var remote)
                && Version.TryParse(TrimVersion(localVersion), out var local)
                && remote > local;
        }

        private static string TrimVersion(string value)
        {
            return (value ?? string.Empty).Trim().TrimStart('v', 'V');
        }

        private static string SafePathSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
```

- [ ] **步骤 6：运行验证**

运行：

```powershell
dotnet build src\PlugHub.Framework\PlugHub.Framework.csproj -c Release
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：`PlugHub.Framework` build PASS；静态验证仍 FAIL，错误指向缺少 `PlugHub.Updater` 或 UI/release 打包。

- [ ] **步骤 7：Commit**

```powershell
git add src\PlugHub.Framework\Updates src\PlugHub.StaticValidation\Program.cs
git commit -m "feat(框架更新): 添加 release 检查和下载服务"
```

---

### 任务 3：实现静默 DLL updater

**文件：**
- 创建：`src/PlugHub.Updater/PlugHub.Updater.csproj`
- 创建：`src/PlugHub.Updater/Program.cs`
- 创建：`src/PlugHub.Updater/UpdaterArguments.cs`
- 创建：`src/PlugHub.Updater/FrameworkDllUpdater.cs`
- 创建：`src/PlugHub.Updater/UpdaterLogger.cs`
- 修改：`PlugHub.sln`
- 修改：`PlugHub.slnx`

- [ ] **步骤 1：创建 updater 项目**

创建 `src/PlugHub.Updater/PlugHub.Updater.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\..\build\Directory.Build.props" />
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>PlugHub.Updater</AssemblyName>
    <RootNamespace>PlugHub.Updater</RootNamespace>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.IO.Compression" />
    <Reference Include="System.IO.Compression.FileSystem" />
  </ItemGroup>
</Project>
```

- [ ] **步骤 2：实现参数解析**

创建 `UpdaterArguments.cs`，支持普通参数和 Base64 变体：

```csharp
using System;
using System.Linq;
using System.Text;

namespace PlugHub.Updater
{
    internal sealed class UpdaterArguments
    {
        public string PayloadZip { get; private set; } = string.Empty;
        public string InstallDirectory { get; private set; } = string.Empty;
        public string TargetVersion { get; private set; } = string.Empty;
        public int RevitProcessId { get; private set; }

        public static UpdaterArguments Parse(string[] args)
        {
            var parsed = new UpdaterArguments
            {
                PayloadZip = FirstValue(args, "/payloadZipBase64", true, FirstValue(args, "/payloadZip", false, string.Empty)),
                InstallDirectory = FirstValue(args, "/installDirBase64", true, FirstValue(args, "/installDir", false, string.Empty)),
                TargetVersion = FirstValue(args, "/targetVersion", false, string.Empty)
            };
            int.TryParse(FirstValue(args, "/revitProcessId", false, "0"), out var processId);
            parsed.RevitProcessId = processId;
            return parsed;
        }

        private static string FirstValue(string[] args, string name, bool decodeBase64, string fallback)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) continue;
                var value = TrimWrappingQuotes(args[index + 1]);
                return decodeBase64 ? Encoding.UTF8.GetString(Convert.FromBase64String(value)) : value;
            }

            return fallback;
        }

        private static string TrimWrappingQuotes(string value)
        {
            value = (value ?? string.Empty).Trim();
            while (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }

            return value;
        }
    }
}
```

- [ ] **步骤 3：实现 logger**

创建 `UpdaterLogger.cs`：

```csharp
using System;
using System.IO;
using System.Text;

namespace PlugHub.Updater
{
    internal sealed class UpdaterLogger
    {
        private readonly string _installDirectory;

        public UpdaterLogger(string installDirectory)
        {
            _installDirectory = installDirectory ?? string.Empty;
        }

        public void Info(string message)
        {
            Write("INFO", message, string.Empty);
        }

        public void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex?.ToString() ?? string.Empty);
        }

        private void Write(string severity, string message, string exception)
        {
            var directory = ResolveLogsDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "plughub-updater-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
            var line = string.Join("\t", DateTime.UtcNow.ToString("o"), severity, Normalize(message), Normalize(exception));
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private string ResolveLogsDirectory()
        {
            var preferred = Path.Combine(_installDirectory, "logs");
            try
            {
                Directory.CreateDirectory(preferred);
                return preferred;
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlugHub",
                    "logs");
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }
    }
}
```

- [ ] **步骤 4：实现 DLL-only updater**

创建 `FrameworkDllUpdater.cs`：

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PlugHub.Updater
{
    internal sealed class FrameworkDllUpdater
    {
        private readonly UpdaterLogger _logger;

        public FrameworkDllUpdater(UpdaterLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(UpdaterArguments args)
        {
            var installDirectory = SafeInstallDirectory(args.InstallDirectory);
            if (!File.Exists(args.PayloadZip)) throw new FileNotFoundException("Payload zip was not found.", args.PayloadZip);
            WaitForRevitExit(args.RevitProcessId);
            CopyFrameworkDllsOnly(args.PayloadZip, installDirectory, args.TargetVersion);
        }

        private void WaitForRevitExit(int processId)
        {
            if (processId <= 0) return;
            try
            {
                var process = Process.GetProcessById(processId);
                _logger.Info("Waiting for Revit process to exit: " + processId);
                process.WaitForExit();
            }
            catch (ArgumentException)
            {
                _logger.Info("Revit process already exited: " + processId);
            }
        }

        private void CopyFrameworkDllsOnly(string payloadZip, string installDirectory, string targetVersion)
        {
            var backupDirectory = Path.Combine(installDirectory, "update-backup", SafeSegment(targetVersion) + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backupDirectory);
            try
            {
                using (var archive = ZipFile.OpenRead(payloadZip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (SkipNonDllEntry(entry.FullName)) continue;
                        var name = Path.GetFileName(entry.FullName);
                        if (string.Equals(name, "PlugHub.addin", StringComparison.OrdinalIgnoreCase)) continue;

                        var targetPath = Path.Combine(installDirectory, name);
                        var backupPath = Path.Combine(backupDirectory, name);
                        if (File.Exists(targetPath))
                        {
                            File.Copy(targetPath, backupPath, true);
                        }

                        entry.ExtractToFile(targetPath, true);
                    }
                }

                _logger.Info("Framework DLL update completed: " + targetVersion);
            }
            catch
            {
                RestoreBackup(backupDirectory, installDirectory);
                throw;
            }
        }

        private static bool SkipNonDllEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return !string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.OrdinalIgnoreCase);
        }

        private static void RestoreBackup(string backupDirectory, string installDirectory)
        {
            if (!Directory.Exists(backupDirectory)) return;
            foreach (var file in Directory.GetFiles(backupDirectory, "*.dll"))
            {
                File.Copy(file, Path.Combine(installDirectory, Path.GetFileName(file)), true);
            }
        }

        private static string SafeInstallDirectory(string installDirectory)
        {
            var full = Path.GetFullPath(installDirectory ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(full) || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to update an unsafe install directory: " + installDirectory);
            }

            return full;
        }

        private static string SafeSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
    }
}
```

- [ ] **步骤 5：实现静默入口**

创建 `Program.cs`：

```csharp
using System;

namespace PlugHub.Updater
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var parsed = UpdaterArguments.Parse(args);
            var logger = new UpdaterLogger(parsed.InstallDirectory);
            try
            {
                new FrameworkDllUpdater(logger).Run(parsed);
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error("Framework update failed.", ex);
                return 1;
            }
        }
    }
}
```

- [ ] **步骤 6：加入 solution**

修改 `PlugHub.sln`，新增 project 和配置。使用固定 GUID 示例：

```text
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PlugHub.Updater", "src\PlugHub.Updater\PlugHub.Updater.csproj", "{95B1A099-6587-45D0-8B05-2434F5D413F8}"
EndProject
```

在 `ProjectConfigurationPlatforms` 和 `NestedProjects` 增加同 GUID 的 Debug/Release 配置，并把项目嵌套到 `{AA0F283E-8C3E-4D06-9F4C-B3C0E3618840}`。

修改 `PlugHub.slnx`：

```xml
  <Project Path="src/PlugHub.Updater/PlugHub.Updater.csproj" />
```

- [ ] **步骤 7：运行验证**

运行：

```powershell
dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：updater build PASS；静态验证仍 FAIL，错误指向 UI、build script 或 workflow。

- [ ] **步骤 8：Commit**

```powershell
git add src\PlugHub.Updater PlugHub.sln PlugHub.slnx
git commit -m "feat(框架更新): 添加静默 DLL 更新器"
```

---

### 任务 4：接入关于页两个按钮

**文件：**
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
- 修改：`src/PlugHub.Revit2020/PlugHub.Revit2020.csproj`

- [ ] **步骤 1：添加 using 和字段**

在 `FrameworkSettingsWindow.cs` 顶部加入：

```csharp
using System.Diagnostics;
using PlugHub.Framework.Updates;
```

在字段区加入：

```csharp
private readonly FrameworkUpdateService _frameworkUpdateService = new FrameworkUpdateService();
private FrameworkUpdateCheckResult? _latestFrameworkUpdate;
private Button? _updateFrameworkButton;
```

- [ ] **步骤 2：构建关于页按钮区**

在 `BuildAboutTab()` 的 `summary.Children.Add(metrics);` 后加入：

```csharp
summary.Children.Add(BuildFrameworkUpdateActions());
```

新增方法：

```csharp
private UIElement BuildFrameworkUpdateActions()
{
    var panel = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 16, 0, 0)
    };
    panel.Children.Add(CreateButton("检查更新", (sender, args) => CheckFrameworkUpdate()));
    _updateFrameworkButton = CreateButton("更新框架", (sender, args) => UpdateFramework());
    _updateFrameworkButton.IsEnabled = false;
    _updateFrameworkButton.Margin = new Thickness(8, 0, 0, 0);
    panel.Children.Add(_updateFrameworkButton);
    return panel;
}
```

- [ ] **步骤 3：实现检查更新事件**

新增：

```csharp
private void CheckFrameworkUpdate()
{
    RefreshStatus("正在检查框架更新，请稍候。");
    Task.Run(() =>
    {
        try
        {
            return _frameworkUpdateService.Check(AssemblyVersionText());
        }
        catch (Exception ex)
        {
            return new FrameworkUpdateCheckResult { Success = false, Message = "检查更新失败：" + ex.Message };
        }
    }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
    {
        _latestFrameworkUpdate = task.Result;
        if (_updateFrameworkButton != null)
        {
            _updateFrameworkButton.IsEnabled = task.Result.Success && task.Result.HasUpdate;
        }

        RefreshStatus(task.Result.Message);
    })));
}
```

- [ ] **步骤 4：实现更新框架事件**

新增：

```csharp
private void UpdateFramework()
{
    if (_latestFrameworkUpdate == null || !_latestFrameworkUpdate.Success || !_latestFrameworkUpdate.HasUpdate)
    {
        RefreshStatus("请先检查更新。");
        return;
    }

    if (_updateFrameworkButton != null) _updateFrameworkButton.IsEnabled = false;
    RefreshStatus("正在下载框架更新，请稍候。");
    var baseDirectory = BaseDirectory();
    var processId = Process.GetCurrentProcess().Id;
    var checkResult = _latestFrameworkUpdate;
    Task.Run(() =>
    {
        try
        {
            var download = _frameworkUpdateService.Download(checkResult);
            if (!download.Success) return new FrameworkUpdateOperationResult { Success = false, Message = download.Message };
            return _frameworkUpdateService.StartUpdater(baseDirectory, download.PackagePath, download.LatestVersion, processId);
        }
        catch (Exception ex)
        {
            return new FrameworkUpdateOperationResult { Success = false, Message = "更新框架失败：" + ex.Message };
        }
    }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
    {
        RefreshStatus(task.Result.Message);
        if (_updateFrameworkButton != null && !task.Result.Success)
        {
            _updateFrameworkButton.IsEnabled = true;
        }
    })));
}
```

- [ ] **步骤 5：运行编译和验证**

运行：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj -c Release /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：Revit 项目 build PASS；静态验证仍 FAIL，错误指向 build script/workflow/docs。

- [ ] **步骤 6：Commit**

```powershell
git add src\PlugHub.Revit2020\FrameworkSettingsWindow.cs
git commit -m "feat(设置): 在关于页添加框架更新入口"
```

---

### 任务 5：打包 updater 到本地输出和 release zip

**文件：**
- 修改：`scripts/build-revit2020.ps1`
- 修改：`.github/workflows/release.yml`
- 修改：`.gitee/workflows/release.yml`

- [ ] **步骤 1：本地构建脚本加入 updater**

在 `scripts/build-revit2020.ps1` 顶部 `$Project` 后加入：

```powershell
$UpdaterProject = Join-Path $Root "src\PlugHub.Updater\PlugHub.Updater.csproj"
```

在 `$Clean` 分支中加入：

```powershell
Remove-RepoPath (Join-Path $Root "src\PlugHub.Updater\bin")
Remove-RepoPath (Join-Path $Root "src\PlugHub.Updater\obj")
```

在主项目 `dotnet build` 成功且 `-NoStage` 返回前后处理：

```powershell
if (!$NoStage) {
    & dotnet build $UpdaterProject -c $Configuration -t:Rebuild "/p:OutDir=$OutputDir\"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build PlugHub.Updater failed with exit code $LASTEXITCODE."
    }
}
```

在 staged 输出检查区加入：

```powershell
$Updater = Join-Path $OutputDir "PlugHub.Updater.exe"
if (!(Test-Path $Updater)) { throw "Build finished but $Updater was not found." }
```

- [ ] **步骤 2：GitHub release workflow 在打包前构建 updater**

在 `.github/workflows/release.yml` 的 `Package release artifact` 步骤前加入：

```yaml
      - name: Build PlugHub updater
        shell: pwsh
        run: |
          $updaterOutput = (Resolve-Path "dist\Revit2020").Path
          dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release -t:Rebuild /p:OutDir="$updaterOutput\"
          $builtUpdater = Join-Path $updaterOutput "PlugHub.Updater.exe"
          if (!(Test-Path $builtUpdater)) {
            throw "Updater build finished but $builtUpdater was not found."
          }
```

- [ ] **步骤 3：Gitee release workflow 在打包前构建 updater**

在 `.gitee/workflows/release.yml` 的 `Package release artifact` 步骤前加入同名步骤：

```yaml
      - name: Build PlugHub updater
        shell: pwsh
        run: |
          $updaterOutput = (Resolve-Path "dist\Revit2020").Path
          dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release -t:Rebuild /p:OutDir="$updaterOutput\"
          $builtUpdater = Join-Path $updaterOutput "PlugHub.Updater.exe"
          if (!(Test-Path $builtUpdater)) {
            throw "Updater build finished but $builtUpdater was not found."
          }
```

- [ ] **步骤 4：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release
```

预期：静态验证仍可能 FAIL，错误只剩 README/development 文档。

- [ ] **步骤 5：Commit**

```powershell
git add scripts\build-revit2020.ps1 .github\workflows\release.yml .gitee\workflows\release.yml
git commit -m "ci(框架更新): 将更新器打入发布包"
```

---

### 任务 6：更新文档并让静态验证转绿

**文件：**
- 修改：`README.md`
- 修改：`docs/development.md`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：更新 README**

在 `README.md` 的「安装包」段落后加入：

```markdown
## 框架更新

设置窗口的「关于」页签提供 `检查更新` 和 `更新框架` 两个按钮。

- `检查更新`：查询 `GaoMengGu/PlugHub` 的 latest release，并比较当前框架版本。
- `更新框架`：发现新版本后下载 `PlugHub-Revit2020-<tag>.zip`，启动静默 updater，并在左下角提示需要重启 Revit。

框架更新只覆盖框架 DLL，不覆盖 `PlugHub.addin`、`packages`、`config`、缓存和日志。当前 Revit 会话不会热替换已加载 DLL；关闭并重新打开 Revit 后，新框架 DLL 才会生效。
```

- [ ] **步骤 2：更新 development 文档**

在 `docs/development.md` 的 Release 安装程序段落后加入：

```markdown
## 框架自动更新

设置窗口「关于」页签包含 `检查更新` 和 `更新框架`。

- `检查更新` 只访问 release 元数据，不修改本地文件。
- `更新框架` 下载 release zip，校验核心 DLL 后启动 `PlugHub.Updater.exe`。
- updater 静默等待当前 Revit 进程退出，只覆盖安装目录根部的框架 DLL。
- updater 不重写 `PlugHub.addin`，不覆盖 `packages`、`config`、`repository-cache`、`runtime-cache` 和 `logs`。

非 Revit 环境只能验证静态规则、构建、zip 校验和临时目录 DLL 覆盖流程，不能声明 Revit 实机测试成功。
```

- [ ] **步骤 3：补充静态验证运行时夹具**

在 `ValidateFrameworkAutoUpdateSpecification()` 中追加轻量行为检查，创建临时 zip 调用 validator：

```csharp
ValidateFrameworkUpdatePackageRejectsUnsafeZip();
```

新增方法：

```csharp
private static void ValidateFrameworkUpdatePackageRejectsUnsafeZip()
{
    var validator = new PlugHub.Framework.Updates.FrameworkUpdatePackageValidator();
    var temp = Path.Combine(Path.GetTempPath(), "PlugHubStaticValidation", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temp);
    try
    {
        var zip = Path.Combine(temp, "unsafe.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntry("../PlugHub.Revit2020.dll");
        }

        var failed = false;
        try
        {
            validator.Validate(zip);
        }
        catch (InvalidDataException)
        {
            failed = true;
        }

        Require(failed, "framework update validator must reject unsafe zip entry paths.");
    }
    finally
    {
        Directory.Delete(temp, true);
    }
}
```

同时给 `Program.cs` 顶部补充：

```csharp
using System.IO.Compression;
```

- [ ] **步骤 4：运行静态验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：PASS，输出包含 `passed: modules=0, features=0, views=1, presets=0`。

- [ ] **步骤 5：Commit**

```powershell
git add README.md docs\development.md src\PlugHub.StaticValidation\Program.cs
git commit -m "docs(框架更新): 记录静默更新边界"
```

---

### 任务 7：最终构建验证

**文件：**
- 验证：`src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj`
- 验证：`src/PlugHub.Updater/PlugHub.Updater.csproj`
- 验证：`src/PlugHub.Revit2020/PlugHub.Revit2020.csproj`
- 验证：`scripts/build-revit2020.ps1`

- [ ] **步骤 1：运行静态验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：PASS。

- [ ] **步骤 2：构建 updater**

运行：

```powershell
dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release
```

预期：PASS，输出 `PlugHub.Updater.exe`。

- [ ] **步骤 3：构建 Revit 适配层，不刷新 dist**

运行：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj -c Release /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

预期：PASS。

- [ ] **步骤 4：运行 Revit 2020 本地打包脚本**

运行：

```powershell
.\scripts\build-revit2020.ps1 -UseRevitApiNuGet -UseRelativeAddinAssembly
```

预期：PASS，`dist\Revit2020` 中存在：

```text
PlugHub.Revit2020.dll
PlugHub.Framework.dll
PlugHub.Contracts.dll
PlugHub.Updater.exe
PlugHub.addin
```

- [ ] **步骤 5：检查 git 状态**

运行：

```powershell
git status --short
```

预期：只允许有实现源码、脚本、workflow、文档变更；不得提交 `bin/`、`obj/`、`dist/`。

- [ ] **步骤 6：Commit 或补充修正**

如果步骤 1-5 暴露遗漏，先修正并重复验证。若没有新的源码变更，不需要额外 commit。

---

## 执行注意事项

- 任何时候都不要让 `PlugHub.Revit2020` 当前进程直接覆盖框架 DLL。
- updater 只能复制 release zip 根目录下的 `.dll` 文件；不得递归复制 `packages`、`config`、缓存、日志或 addin。
- `检查更新` 访问网络；静态验证不要依赖真实网络。
- 在非 Revit 环境中，只能声明静态验证和构建通过，不能声明 Revit 实机加载或 Ribbon 行为通过。
- 如果本机 sandbox 因 `C:\Users\Yilan\AppData\Local\Microsoft SDKs` 权限导致 `dotnet run` 失败，按既有策略在沙箱外重跑同一验证命令。
