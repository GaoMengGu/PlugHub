# PlugHub V1.2 Architecture Hardening 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 实现 V1.2 架构整理、安全补强和诊断补强，同时保持 Revit 2020 主线稳定。

**架构：** 先降低构建和验证风险，再用 facade 保持外部调用稳定，逐步拆分包生命周期服务和设置页。安全、日志、异常兜底作为横向能力接入，不引入 Revit 2020 ALC、AppDomain 沙箱或强权限隔离。

**技术栈：** C# net48、WPF、PowerShell、MSBuild、JavaScriptSerializer、Windows DPAPI、GitHub Actions、PlugHub.StaticValidation。

---

## 文件结构

### 构建和脚本

- 修改：`src/PlugHub.Revit2020/PlugHub.Revit2020.csproj`
  - 增加 `StagePlugHubOutput` MSBuild 属性。
  - 给 `StagePlugHubOutput` target 增加 condition。
- 修改：`scripts/build-revit2020.ps1`
  - 增加 `-Clean`、`-CleanAddin`、`-NoStage` 参数。
  - 增加仓库内路径校验和清理函数。
  - 安装 addin 时使用备份和回滚函数。
- 修改：`scripts/install-addin.ps1`
  - 增加 `-Silent` 和备份回滚。

### 静态验证

- 创建：`src/PlugHub.StaticValidation/Validation/ValidationSeverity.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationIssue.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationReport.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`
  - 支持 `--report-json <path>` 和 `--report-html <path>`。
  - 保留默认控制台输出和退出码。

### 配置与 schema

- 创建：`config/schemas/package.schema.json`
- 修改：`config/schemas/sources.schema.json`
  - 仓库配置保留 sources schema 职责，不继续承担完整 package schema。
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
  - 增加包兼容字段和凭据密文字段。
- 修改：`src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs`
  - 在发现阶段跳过不兼容的 package/module。

### 包生命周期服务

- 创建：`src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs`
- 创建：`src/PlugHub.Framework/Packages/PackageManifestReader.cs`
- 创建：`src/PlugHub.Framework/Packages/PackageInstallService.cs`
- 创建：`src/PlugHub.Framework/Packages/RepositoryBrowser.cs`
- 创建：`src/PlugHub.Framework/Packages/RepositoryCredentialService.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs`
- 修改：`src/PlugHub.Framework/Packages/PackageRepositoryService.cs`
  - 保留 public facade。
  - 把读写 pending operations、manifest 解析、install payload、git browse 逐步委托给新类。

### 设置 UI

- 创建：`src/PlugHub.Revit2020/Settings/Rows/ModuleRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/FeatureRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/GroupRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RepositoryRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RepositoryPackageRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/PendingPackageOperationRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/DiagnosticRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs`
- 创建：`src/PlugHub.Revit2020/Settings/SettingsConfigurationStore.cs`
- 创建：`src/PlugHub.Revit2020/Settings/RepositorySettingsController.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
  - 逐步删除嵌套 row model。
  - 添加待处理操作列表和取消操作入口。

### 日志和稳定性

- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogEntry.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs`
- 修改：`src/PlugHub.Contracts/Modules/DiagnosticMessage.cs`
  - 如需兼容 timestamp 和 operation，优先新增属性并保持默认值。
- 修改：`src/PlugHub.Revit2020/FeatureCommandDispatcher.cs`
  - 捕获业务命令 `Execute` 的普通异常。
- 修改：`src/PlugHub.Revit2020/FrameworkStatusWindow.cs`
  - 日志展示接收新增字段时仍兼容旧字段。

### 文档

- 修改：`docs/architecture.md`
- 修改：`docs/development.md`
- 修改：`docs/project-overview.md`
- 修改：`docs/README.md`
- 创建：`docs/plugin-development.md`

---

### 任务 1：构建 staging 开关、clean 和 addin 安装回滚

**文件：**
- 修改：`src/PlugHub.Revit2020/PlugHub.Revit2020.csproj`
- 修改：`scripts/build-revit2020.ps1`
- 修改：`scripts/install-addin.ps1`
- 修改：`src/PlugHub.StaticValidation/Program.cs`
- 测试：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `ValidateRevitApiReferenceStrategy()` 中增加断言：

```csharp
Require(revitProject.Contains("<StagePlugHubOutput Condition=\"'$(StagePlugHubOutput)' == ''\">true</StagePlugHubOutput>"), "Revit project must default StagePlugHubOutput to true.");
Require(revitProject.Contains("Condition=\"'$(StagePlugHubOutput)' == 'true'\""), "StagePlugHubOutput target must be guarded by StagePlugHubOutput=true.");
Require(buildScript.Contains("[switch]$NoStage"), "build script must expose -NoStage.");
Require(buildScript.Contains("[switch]$Clean"), "build script must expose -Clean.");
Require(buildScript.Contains("Assert-PathInsideRoot"), "build script must verify clean targets stay inside the repository.");
Require(installScript.Contains("Backup-ExistingAddin") && installScript.Contains("Restore-AddinBackup"), "addin install script must backup and restore the addin manifest.");
```

如果 `installScript` 未定义，在方法开头加入：

```csharp
var installScript = ReadText("scripts/install-addin.ps1");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `StagePlugHubOutput` 或 `build script must expose -NoStage`。

- [ ] **步骤 3：修改 Revit 项目 staging 条件**

在 `src/PlugHub.Revit2020/PlugHub.Revit2020.csproj` 的主 `PropertyGroup` 中加入：

```xml
<StagePlugHubOutput Condition="'$(StagePlugHubOutput)' == ''">true</StagePlugHubOutput>
```

把 target 改为：

```xml
<Target Name="StagePlugHubOutput" AfterTargets="Build" Condition="'$(StagePlugHubOutput)' == 'true'">
```

- [ ] **步骤 4：修改 build 脚本参数和清理函数**

在 `scripts/build-revit2020.ps1` 参数中加入：

```powershell
[switch]$NoStage,
[switch]$Clean,
[switch]$CleanAddin
```

在 `$Root` 定义后加入：

```powershell
function Assert-PathInsideRoot {
    param([string]$Path)
    $resolvedRoot = (Resolve-Path $Root).Path.TrimEnd("\")
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
    if (!$fullPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository: $fullPath"
    }
}

function Remove-RepoPath {
    param([string]$Path)
    if (Test-Path $Path) {
        Assert-PathInsideRoot $Path
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}
```

在构建前加入：

```powershell
if ($Clean) {
    Remove-RepoPath (Join-Path $Root "dist\Revit2020")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Contracts\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Contracts\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Framework\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Framework\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Revit2020\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Revit2020\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.StaticValidation\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.StaticValidation\obj")
}
```

在 `$buildArguments` 组装后加入：

```powershell
if ($NoStage) {
    $buildArguments += "/p:StagePlugHubOutput=false"
}
```

- [ ] **步骤 5：修改 addin 安装回滚**

在 `scripts/install-addin.ps1` 中加入：

```powershell
[switch]$Silent
```

加入函数：

```powershell
function Backup-ExistingAddin {
    param([string]$Target)
    if (!(Test-Path $Target)) { return "" }
    $backup = "$Target.bak"
    Copy-Item -LiteralPath $Target -Destination $backup -Force
    return $backup
}

function Restore-AddinBackup {
    param([string]$Target, [string]$Backup)
    if (![string]::IsNullOrWhiteSpace($Backup) -and (Test-Path $Backup)) {
        Copy-Item -LiteralPath $Backup -Destination $Target -Force
    }
}
```

把复制逻辑改为：

```powershell
$TargetAddin = Join-Path $AddinsDir "PlugHub.addin"
$Backup = Backup-ExistingAddin $TargetAddin
try {
    Copy-Item $Addin $TargetAddin -Force
}
catch {
    Restore-AddinBackup $TargetAddin $Backup
    throw
}
if (!$Silent) {
    Write-Host "Installed: $TargetAddin"
}
```

- [ ] **步骤 6：运行验证和构建命令**

运行：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：两个命令成功；第二个命令输出 `passed:`。

- [ ] **步骤 7：Commit**

```powershell
git add src/PlugHub.Revit2020/PlugHub.Revit2020.csproj scripts/build-revit2020.ps1 scripts/install-addin.ps1 src/PlugHub.StaticValidation/Program.cs
git commit -m "build: add staging switch and safer addin install"
```

---

### 任务 2：静态验证结构化报告基础

**文件：**
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationSeverity.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationIssue.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationReport.cs`
- 创建：`src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：编写失败验证**

在 `ValidateRequiredFiles()` 的 required 列表加入：

```csharp
"src/PlugHub.StaticValidation/Validation/ValidationSeverity.cs",
"src/PlugHub.StaticValidation/Validation/ValidationIssue.cs",
"src/PlugHub.StaticValidation/Validation/ValidationReport.cs",
"src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs",
```

新增断言：

```csharp
var validationProgram = ReadText("src/PlugHub.StaticValidation/Program.cs");
Require(validationProgram.Contains("string[] args"), "Static validation entrypoint must accept command-line arguments.");
Require(validationProgram.Contains("--report-json") && validationProgram.Contains("--report-html"), "Static validation must support JSON and HTML report arguments.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `ValidationSeverity.cs` 或 `report arguments`。

- [ ] **步骤 3：创建验证模型**

创建 `src/PlugHub.StaticValidation/Validation/ValidationSeverity.cs`：

```csharp
namespace PlugHub.StaticValidation.Validation
{
    internal enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
}
```

创建 `src/PlugHub.StaticValidation/Validation/ValidationIssue.cs`：

```csharp
namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }
}
```

创建 `src/PlugHub.StaticValidation/Validation/ValidationReport.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);

        public void Error(string code, string file, string message, string suggestion)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Code = code,
                File = file,
                Message = message,
                Suggestion = suggestion
            });
        }
    }
}
```

- [ ] **步骤 4：创建报告写入器**

创建 `src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs`：

```csharp
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal static class ValidationReportWriter
    {
        public static void WriteJson(string path, ValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(path, serializer.Serialize(report.Issues));
        }

        public static void WriteHtml(string path, ValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var rows = string.Join("\n", report.Issues.Select(issue =>
                "<tr><td>" + Escape(issue.Severity.ToString()) + "</td><td>" +
                Escape(issue.Code) + "</td><td>" + Escape(issue.File) + "</td><td>" +
                Escape(issue.Message) + "</td><td>" + Escape(issue.Suggestion) + "</td></tr>"));
            File.WriteAllText(path,
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>PlugHub Validation</title></head><body><table>" +
                rows +
                "</table></body></html>",
                Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
```

- [ ] **步骤 5：修改入口参数**

把 `Main()` 改为：

```csharp
private static int Main(string[] args)
```

在 `try` 末尾、`Console.WriteLine(...)` 前加入：

```csharp
var report = new PlugHub.StaticValidation.Validation.ValidationReport();
WriteReports(args, report);
```

在 `catch` 中加入：

```csharp
var report = new PlugHub.StaticValidation.Validation.ValidationReport();
report.Error("PH-VALIDATION-FAILED", string.Empty, ex.Message, "Read the failing validation message and update the referenced PlugHub file.");
WriteReports(args, report);
```

在 `Program` 中新增：

```csharp
private static void WriteReports(string[] args, PlugHub.StaticValidation.Validation.ValidationReport report)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (args[index] == "--report-json" && index + 1 < args.Length)
        {
            PlugHub.StaticValidation.Validation.ValidationReportWriter.WriteJson(args[index + 1], report);
        }

        if (args[index] == "--report-html" && index + 1 < args.Length)
        {
            PlugHub.StaticValidation.Validation.ValidationReportWriter.WriteHtml(args[index + 1], report);
        }
    }
}
```

- [ ] **步骤 6：运行报告命令**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj -- --report-json build\validation-report.json --report-html build\validation-report.html
```

预期：命令成功；`build\validation-report.json` 和 `build\validation-report.html` 存在。

- [ ] **步骤 7：Commit**

```powershell
git add src/PlugHub.StaticValidation
git commit -m "test: add static validation reports"
```

---

### 任务 3：包 schema 与兼容字段验证

**文件：**
- 创建：`config/schemas/package.schema.json`
- 创建：`src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs`
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
- 修改：`src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `ValidateRequiredFiles()` 的 required 列表加入：

```csharp
"config/schemas/package.schema.json",
"src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs",
```

在 `ValidateContractsMultiTargetReadiness()` 后新增调用：

```csharp
ValidatePackageManifestSchemaAndCompatibility();
```

新增方法：

```csharp
private static void ValidatePackageManifestSchemaAndCompatibility()
{
    var schema = ReadText("config/schemas/package.schema.json");
    Require(schema.Contains("\"revitVersions\""), "package schema must define revitVersions.");
    Require(schema.Contains("\"frameworkVersionRange\""), "package schema must define frameworkVersionRange.");
    var models = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
    Require(models.Contains("RevitVersions") && models.Contains("FrameworkVersionRange"), "configuration models must expose package compatibility fields.");
    var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
    Require(discovery.Contains("IsCompatibleWithRuntime"), "module discovery must skip packages incompatible with the active runtime.");
}
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `package.schema.json`。

- [ ] **步骤 3：添加 package schema**

创建 `config/schemas/package.schema.json`：

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["schemaVersion", "modules"],
  "properties": {
    "schemaVersion": { "type": "string" },
    "version": { "type": "string" },
    "revitVersions": { "type": "array", "items": { "type": "string" } },
    "frameworkVersionRange": { "type": "string" },
    "sha256": { "type": "string" },
    "signature": { "type": "string" },
    "modules": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "enabled", "visible", "features"],
        "properties": {
          "id": { "type": "string" },
          "assembly": { "type": "string" },
          "type": { "type": "string" },
          "displayName": { "type": "string" },
          "enabled": { "type": "boolean" },
          "visible": { "type": "boolean" },
          "order": { "type": "integer" },
          "features": { "type": "array" }
        }
      }
    }
  }
}
```

- [ ] **步骤 4：添加配置字段**

在 `ModulesConfiguration` 中加入：

```csharp
public string Version { get; set; } = string.Empty;
public List<string> RevitVersions { get; set; } = new List<string>();
public string FrameworkVersionRange { get; set; } = string.Empty;
public string Sha256 { get; set; } = string.Empty;
public string Signature { get; set; } = string.Empty;
```

在 `ModuleConfiguration` 中加入：

```csharp
public List<string> RevitVersions { get; set; } = new List<string>();
public string FrameworkVersionRange { get; set; } = string.Empty;
```

在 `CloneModules()` 中复制这些字段：

```csharp
Version = modules.Version,
RevitVersions = new List<string>(modules.RevitVersions ?? new List<string>()),
FrameworkVersionRange = modules.FrameworkVersionRange,
Sha256 = modules.Sha256,
Signature = modules.Signature,
```

- [ ] **步骤 5：发现阶段兼容性拦截**

在 `ModuleSourceResolver.TryReadPlugHubManifest` 反序列化后，把 root compatibility 字段下推到 module：

```csharp
foreach (var module in modules.Modules ?? new List<ModuleConfiguration>())
{
    if ((module.RevitVersions == null || module.RevitVersions.Count == 0) && modules.RevitVersions != null)
    {
        module.RevitVersions = new List<string>(modules.RevitVersions);
    }

    if (string.IsNullOrWhiteSpace(module.FrameworkVersionRange))
    {
        module.FrameworkVersionRange = modules.FrameworkVersionRange ?? string.Empty;
    }
}
```

在 `ModuleDiscoveryService` 增加 helper：

```csharp
private static bool IsCompatibleWithRuntime(ModuleConfiguration module, out string reason)
{
    reason = string.Empty;
    if (module == null) return true;
    var revitVersions = module.RevitVersions ?? new List<string>();
    if (revitVersions.Count > 0 && !revitVersions.Contains("2020"))
    {
        reason = "Module does not declare compatibility with Revit 2020.";
        return false;
    }

    return true;
}
```

在发现循环里调用该 helper；不兼容时跳过该 module，并写入 warning 诊断：

```csharp
if (!IsCompatibleWithRuntime(module, out var compatibilityReason))
{
    diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Warning, "RT-MODULE-COMPATIBILITY", compatibilityReason));
    continue;
}
```

- [ ] **步骤 6：创建包清单验证类**

创建 `src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs`：

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal static class PackageManifestValidation
    {
        public static IEnumerable<ValidationIssue> ValidateFile(string path)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (root == null || !root.ContainsKey("schemaVersion") || !root.ContainsKey("modules"))
            {
                yield return Error(path, "PH-PACKAGE-SCHEMA", "Package manifest must contain schemaVersion and modules.", "Add schemaVersion and a modules array.");
                yield break;
            }

            var modules = root["modules"] as object[];
            if (modules == null || modules.Length == 0)
            {
                yield return Error(path, "PH-PACKAGE-MODULES", "Package manifest must declare at least one module.", "Add one module entry.");
            }
        }

        private static ValidationIssue Error(string file, string code, string message, string suggestion)
        {
            return new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Code = code,
                File = file,
                Message = message,
                Suggestion = suggestion
            };
        }
    }
}
```

- [ ] **步骤 7：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 8：Commit**

```powershell
git add config/schemas/package.schema.json src/PlugHub.Framework/Configuration/ConfigurationModels.cs src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs src/PlugHub.StaticValidation
git commit -m "feat: add package compatibility schema"
```

---

### 任务 4：待处理包操作 store 与取消能力

**文件：**
- 创建：`src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs`
- 修改：`src/PlugHub.Framework/Packages/PackageRepositoryService.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 文件列表加入：

```csharp
"src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs",
```

在 `ValidatePackageSourceAndReleaseBehavior()` 中增加：

```csharp
Require(packageRepositoryService.Contains("ListPendingOperations"), "package repository service must expose pending operation listing.");
Require(packageRepositoryService.Contains("CancelPendingOperation"), "package repository service must expose pending operation cancellation.");
var pendingStore = ReadText("src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs");
Require(pendingStore.Contains("AddOrReplace") && pendingStore.Contains("Remove") && pendingStore.Contains("Read"), "pending operation store must read, add, and remove operations.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `PendingPackageOperationStore.cs`。

- [ ] **步骤 3：创建 PendingPackageOperationStore**

创建 `src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.Framework.Packages
{
    public sealed class PendingPackageOperationStore
    {
        private const string PendingOperationsFileName = "pending-operations.json";
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public IReadOnlyList<PendingPackageOperation> Read(string baseDirectory)
        {
            var path = PathFor(baseDirectory);
            if (!File.Exists(path)) return new List<PendingPackageOperation>();
            try
            {
                var document = _serializer.Deserialize<PendingPackageOperationsDocument>(File.ReadAllText(path));
                return document?.Operations ?? new List<PendingPackageOperation>();
            }
            catch
            {
                return new List<PendingPackageOperation>();
            }
        }

        public void AddOrReplace(string baseDirectory, PendingPackageOperation operation)
        {
            var operations = Read(baseDirectory).Where(item => !SamePackage(item, operation)).ToList();
            operations.Add(operation);
            Write(baseDirectory, operations);
        }

        public void Remove(string baseDirectory, string packageId, string moduleId)
        {
            var operations = Read(baseDirectory)
                .Where(item => !SamePackage(item, packageId, moduleId))
                .ToList();
            Write(baseDirectory, operations);
        }

        public static string PathFor(string baseDirectory)
        {
            return Path.Combine(baseDirectory, "repository-cache", ".package-install", PendingOperationsFileName);
        }

        private void Write(string baseDirectory, IReadOnlyList<PendingPackageOperation> operations)
        {
            var path = PathFor(baseDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? baseDirectory);
            if (operations.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            File.WriteAllText(path, _serializer.Serialize(new PendingPackageOperationsDocument { Operations = operations.ToList() }));
        }

        private static bool SamePackage(PendingPackageOperation left, PendingPackageOperation right)
        {
            return SamePackage(left, right.PackageId, right.ModuleId);
        }

        private static bool SamePackage(PendingPackageOperation operation, string packageId, string moduleId)
        {
            return string.Equals(operation.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **步骤 4：接入 facade**

在 `PackageRepositoryService` 增加字段：

```csharp
private readonly PendingPackageOperationStore _pendingOperations = new PendingPackageOperationStore();
```

新增 public 方法：

```csharp
public IReadOnlyList<PendingPackageOperation> ListPendingOperations(string baseDirectory)
{
    return _pendingOperations.Read(baseDirectory);
}

public PackageRepositoryOperationResult CancelPendingOperation(string baseDirectory, string packageId, string moduleId)
{
    var operation = _pendingOperations.Read(baseDirectory)
        .FirstOrDefault(item => string.Equals(item.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
    if (operation == null) return PackageRepositoryOperationResult.Failed("未找到待处理插件包操作: " + packageId);
    if (string.Equals(operation.Operation, "update", StringComparison.OrdinalIgnoreCase))
    {
        DeleteDirectoryQuietly(operation.StagingDirectory);
    }

    _pendingOperations.Remove(baseDirectory, packageId, moduleId);
    return PackageRepositoryOperationResult.Succeeded("已取消待处理插件包操作: " + packageId);
}
```

把现有 `QueuePendingOperation`、`RemovePendingOperations`、`ReadPendingOperations`、`WritePendingOperations` 内部委托到 store。保留方法名，降低当前类内改动量。

- [ ] **步骤 5：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 6：Commit**

```powershell
git add src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs src/PlugHub.Framework/Packages/PackageRepositoryService.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: expose pending package operation cancellation"
```

---

### 任务 5：仓库凭据保护与诊断脱敏

**文件：**
- 创建：`src/PlugHub.Framework/Packages/RepositoryCredentialService.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs`
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
- 修改：`src/PlugHub.Framework/Packages/PackageRepositoryService.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 列表加入：

```csharp
"src/PlugHub.Framework/Packages/RepositoryCredentialService.cs",
"src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs",
```

在 `ValidatePackageSourceAndReleaseBehavior()` 中增加：

```csharp
var credentialService = ReadText("src/PlugHub.Framework/Packages/RepositoryCredentialService.cs");
Require(credentialService.Contains("ProtectedData.Protect") && credentialService.Contains("ProtectedData.Unprotect"), "repository credential service must use DPAPI.");
var redactor = ReadText("src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs");
Require(redactor.Contains("Redact") && redactor.Contains("x-access-token") && redactor.Contains("oauth2"), "diagnostic redactor must mask repository tokens.");
var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
Require(configurationModels.Contains("EncryptedApiKey"), "repository configuration must persist encrypted apiKey separately.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `RepositoryCredentialService.cs`。

- [ ] **步骤 3：扩展配置模型**

在 `PackageRepositoryConfiguration` 加入：

```csharp
public string EncryptedApiKey { get; set; } = string.Empty;
public string ApiKeyProtection { get; set; } = string.Empty;
```

- [ ] **步骤 4：创建凭据服务**

创建 `src/PlugHub.Framework/Packages/RepositoryCredentialService.cs`：

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryCredentialService
    {
        public string ResolveApiKey(PackageRepositoryConfiguration repository)
        {
            if (repository == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(repository.EncryptedApiKey)
                && string.Equals(repository.ApiKeyProtection, "dpapi-current-user", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = Convert.FromBase64String(repository.EncryptedApiKey);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
            }

            return repository.ApiKey ?? string.Empty;
        }

        public void ProtectForSave(PackageRepositoryConfiguration repository)
        {
            if (repository == null || string.IsNullOrWhiteSpace(repository.ApiKey)) return;
            var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(repository.ApiKey), null, DataProtectionScope.CurrentUser);
            repository.EncryptedApiKey = Convert.ToBase64String(protectedBytes);
            repository.ApiKeyProtection = "dpapi-current-user";
            repository.ApiKey = string.Empty;
        }
    }
}
```

- [ ] **步骤 5：创建脱敏器**

创建 `src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs`：

```csharp
using System.Text.RegularExpressions;

namespace PlugHub.Framework.Diagnostics
{
    public static class SensitiveTextRedactor
    {
        public static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var redacted = Regex.Replace(value, "https://oauth2:[^@]+@", "https://oauth2:***@", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, "https://x-access-token:[^@]+@", "https://x-access-token:***@", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, "(apiKey\"?\\s*[:=]\\s*\"?)[^\"\\s,]+", "$1***", RegexOptions.IgnoreCase);
            return redacted;
        }
    }
}
```

- [ ] **步骤 6：接入仓库 URL 生成**

在 `PackageRepositoryService` 增加字段：

```csharp
private readonly RepositoryCredentialService _credentialService = new RepositoryCredentialService();
```

把 `RepositoryUrl` 从 static 改为实例方法，并用：

```csharp
var apiKey = _credentialService.ResolveApiKey(repository);
```

替换直接访问 `repository.ApiKey` 的判断和拼接。

- [ ] **步骤 7：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 8：Commit**

```powershell
git add src/PlugHub.Framework/Configuration/ConfigurationModels.cs src/PlugHub.Framework/Packages/RepositoryCredentialService.cs src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs src/PlugHub.Framework/Packages/PackageRepositoryService.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: protect repository credentials"
```

---

### 任务 6：插件执行异常兜底和文件日志

**文件：**
- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogEntry.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs`
- 创建：`src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs`
- 修改：`src/PlugHub.Revit2020/FeatureCommandDispatcher.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 列表加入：

```csharp
"src/PlugHub.Framework/Diagnostics/PlugHubLogEntry.cs",
"src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs",
"src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs",
```

在 `ValidateRuntimeRoutingSpecification()` 增加：

```csharp
Require(featureDispatcher.Contains("catch (Exception ex)") && featureDispatcher.Contains("PH-COMMAND-EXECUTE"), "FeatureCommandDispatcher must catch business command Execute exceptions.");
var logger = ReadText("src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs");
Require(logger.Contains("plughub-") && logger.Contains(".log"), "PlugHub logger must write daily log files.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `PlugHubLogger.cs` 或 `PH-COMMAND-EXECUTE`。

- [ ] **步骤 3：创建日志模型**

创建 `src/PlugHub.Framework/Diagnostics/PlugHubLogEntry.cs`：

```csharp
using System;
using PlugHub.Contracts.Modules;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public DiagnosticSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
    }
}
```

创建 `src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs`：

```csharp
using System;
using System.IO;
using PlugHub.Contracts.Modules;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogger
    {
        public void Write(string baseDirectory, PlugHubLogEntry entry)
        {
            var logDirectory = Path.Combine(baseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            var path = Path.Combine(logDirectory, "plughub-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
            var line = string.Join("\t", new[]
            {
                entry.TimestampUtc.ToString("o"),
                entry.Severity.ToString(),
                entry.Code,
                entry.ModuleId,
                entry.FeatureId,
                entry.Operation,
                SensitiveTextRedactor.Redact(entry.Message),
                SensitiveTextRedactor.Redact(entry.Exception)
            });
            File.AppendAllText(path, line + Environment.NewLine);
        }

        public void Error(string baseDirectory, string code, string moduleId, string featureId, string operation, string message, Exception exception)
        {
            Write(baseDirectory, new PlugHubLogEntry
            {
                Severity = DiagnosticSeverity.Error,
                Code = code,
                ModuleId = moduleId ?? string.Empty,
                FeatureId = featureId ?? string.Empty,
                Operation = operation ?? string.Empty,
                Message = message ?? string.Empty,
                Exception = exception == null ? string.Empty : exception.ToString()
            });
        }
    }
}
```

创建 `src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs`：

```csharp
using System.IO;
using System.IO.Compression;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogExporter
    {
        public void Export(string baseDirectory, string targetZipPath)
        {
            var logsDirectory = Path.Combine(baseDirectory, "logs");
            if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            ZipFile.CreateFromDirectory(logsDirectory, targetZipPath);
        }
    }
}
```

如果 `ZipFile` 不可用，给 `PlugHub.Framework.csproj` 增加：

```xml
<Reference Include="System.IO.Compression.FileSystem" />
```

- [ ] **步骤 4：业务命令 Execute 兜底**

在 `FeatureCommandDispatcher` 增加 using：

```csharp
using PlugHub.Framework.Diagnostics;
```

把最后一行执行改为：

```csharp
try
{
    return command.Execute(commandData, ref message, elements);
}
catch (Exception ex)
{
    message = "PlugHub feature command failed: " + ex.Message;
    new PlugHubLogger().Error(
        FrameworkRuntimeState.BaseDirectory,
        "PH-COMMAND-EXECUTE",
        feature.ModuleId,
        feature.Id,
        "Execute",
        message,
        ex);
    ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-EXECUTE", feature.ModuleId, DiagnosticSeverity.Error);
    return Result.Failed;
}
```

- [ ] **步骤 5：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 6：Commit**

```powershell
git add src/PlugHub.Framework/Diagnostics src/PlugHub.Framework/PlugHub.Framework.csproj src/PlugHub.Revit2020/FeatureCommandDispatcher.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: log plugin command failures"
```

---

### 任务 7：设置页 row model 拆分和待处理操作 UI

**文件：**
- 创建：`src/PlugHub.Revit2020/Settings/Rows/ModuleRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/FeatureRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/GroupRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RepositoryRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RepositoryPackageRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/PendingPackageOperationRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/DiagnosticRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/RepositorySettingsController.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 列表加入所有新 row 文件和 controller 文件。

在 `ValidateSettingsCreationAndSortingSpecification()` 增加：

```csharp
Require(!settingsWindow.Contains("private sealed class RepositoryPackageRow"), "RepositoryPackageRow must be extracted from FrameworkSettingsWindow.");
Require(settingsWindow.Contains("PendingPackageOperationRow"), "settings window must display pending package operations.");
Require(settingsWindow.Contains("CancelSelectedPendingPackageOperation"), "settings window must allow cancelling selected pending package operations.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `RepositoryPackageRow must be extracted`。

- [ ] **步骤 3：移动 row model**

把 `FrameworkSettingsWindow` 中的嵌套 row classes 移到 `src/PlugHub.Revit2020/Settings/Rows`。每个文件使用命名空间：

```csharp
namespace PlugHub.Revit2020.Settings.Rows
{
}
```

在 `FrameworkSettingsWindow.cs` 顶部加入：

```csharp
using PlugHub.Revit2020.Settings.Rows;
```

保持原有 public property 名称不变，避免 DataGrid binding 断裂。

- [ ] **步骤 4：新增 pending operation row**

创建 `PendingPackageOperationRow.cs`：

```csharp
using PlugHub.Framework.Packages;

namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class PendingPackageOperationRow
    {
        public string Operation { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;

        public static PendingPackageOperationRow FromOperation(PendingPackageOperation operation)
        {
            return new PendingPackageOperationRow
            {
                Operation = operation.Operation ?? string.Empty,
                PackageId = operation.PackageId ?? string.Empty,
                ModuleId = operation.ModuleId ?? string.Empty,
                InstallDirectory = operation.InstallDirectory ?? string.Empty,
                CreatedAtUtc = operation.CreatedAtUtc ?? string.Empty
            };
        }
    }
}
```

- [ ] **步骤 5：设置页加载 pending operations**

在 `FrameworkSettingsWindow` 增加字段：

```csharp
private readonly DataGrid _pendingPackageOperationsGrid = CreateGrid();
private ObservableCollection<PendingPackageOperationRow> _pendingPackageOperationRows = new ObservableCollection<PendingPackageOperationRow>();
```

在仓库 tab 构造中把 pending grid 放到仓库包列表下方。列至少包含：

```csharp
_pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.Operation), "操作", true, 0.6));
_pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.PackageId), "插件包", true, 1.2));
_pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.CreatedAtUtc), "创建时间", true, 1.0));
```

新增加载方法：

```csharp
private void LoadPendingPackageOperationRows()
{
    _pendingPackageOperationRows = new ObservableCollection<PendingPackageOperationRow>(
        _packageRepositoryService.ListPendingOperations(BaseDirectory()).Select(PendingPackageOperationRow.FromOperation));
    _pendingPackageOperationsGrid.ItemsSource = _pendingPackageOperationRows;
}
```

在 `LoadRows()` 和包操作后调用 `LoadPendingPackageOperationRows()`。

- [ ] **步骤 6：取消 pending operation**

在 pending grid context menu 中添加：

```csharp
menu.Items.Add(MenuItem("取消待处理操作", (sender, args) => CancelSelectedPendingPackageOperation()));
```

新增方法：

```csharp
private void CancelSelectedPendingPackageOperation()
{
    if (!(_pendingPackageOperationsGrid.SelectedItem is PendingPackageOperationRow row)) return;
    var result = _packageRepositoryService.CancelPendingOperation(BaseDirectory(), row.PackageId, row.ModuleId);
    LoadPendingPackageOperationRows();
    RefreshStatus(result.Message);
}
```

- [ ] **步骤 7：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 8：Commit**

```powershell
git add src/PlugHub.Revit2020/Settings src/PlugHub.Revit2020/FrameworkSettingsWindow.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "refactor: extract settings rows and show pending operations"
```

---

### 任务 8：PackageRepositoryService facade 内部拆分

**文件：**
- 创建：`src/PlugHub.Framework/Packages/RepositoryBrowser.cs`
- 创建：`src/PlugHub.Framework/Packages/PackageManifestReader.cs`
- 创建：`src/PlugHub.Framework/Packages/PackageInstallService.cs`
- 修改：`src/PlugHub.Framework/Packages/PackageRepositoryService.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 列表加入：

```csharp
"src/PlugHub.Framework/Packages/RepositoryBrowser.cs",
"src/PlugHub.Framework/Packages/PackageManifestReader.cs",
"src/PlugHub.Framework/Packages/PackageInstallService.cs",
```

在 `ValidatePackageSourceAndReleaseBehavior()` 增加：

```csharp
Require(packageRepositoryService.Contains("new RepositoryBrowser"), "PackageRepositoryService must delegate browsing to RepositoryBrowser.");
Require(packageRepositoryService.Contains("new PackageManifestReader"), "PackageRepositoryService must delegate manifest reading to PackageManifestReader.");
Require(packageRepositoryService.Contains("new PackageInstallService"), "PackageRepositoryService must delegate payload installation to PackageInstallService.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `RepositoryBrowser.cs`。

- [ ] **步骤 3：创建 RepositoryBrowser 骨架并迁移 Browse**

创建 `RepositoryBrowser.cs`：

```csharp
using System.Collections.Generic;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryBrowser
    {
        public IReadOnlyList<RepositoryPackageDescriptor> Browse(
            string baseDirectory,
            PackageRepositoryConfiguration repository,
            out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            diagnostics = new List<DiagnosticMessage>();
            return new List<RepositoryPackageDescriptor>();
        }
    }
}
```

然后从 `PackageRepositoryService` 迁移 `Browse`、`BrowseCached`、git 同步相关 private methods 到 `RepositoryBrowser`。迁移后 `PackageRepositoryService.Browse` 只调用 `_repositoryBrowser.Browse(...)`。

- [ ] **步骤 4：创建 PackageManifestReader 并迁移清单读取**

创建 `PackageManifestReader.cs`：

```csharp
using System.Collections.Generic;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageManifestReader
    {
        public IReadOnlyList<RepositoryPackageDescriptor> ReadPackagesFromManifest(string manifestPath, string repositoryId, string baseDirectory)
        {
            return new List<RepositoryPackageDescriptor>();
        }
    }
}
```

从 `PackageRepositoryService` 迁移 `ReadPackagesFromManifest`、`ReadModulePackages`、`PackageDisplayName`、manifest dictionary helpers。迁移后 browsing 和 install 都通过 reader 获取标准化 descriptor。

- [ ] **步骤 5：创建 PackageInstallService 并迁移 payload copy**

创建 `PackageInstallService.cs`：

```csharp
namespace PlugHub.Framework.Packages
{
    public sealed class PackageInstallService
    {
        public PackageRepositoryOperationResult InstallPackagePayload(RepositoryPackageDescriptor package, string stagingDirectory)
        {
            if (package == null) throw new System.ArgumentNullException(nameof(package));
            if (string.IsNullOrWhiteSpace(stagingDirectory)) throw new System.ArgumentException("Staging directory is required.", nameof(stagingDirectory));
            return CopyPackagePayload(package, stagingDirectory);
        }

        private PackageRepositoryOperationResult CopyPackagePayload(RepositoryPackageDescriptor package, string stagingDirectory)
        {
            return PackageRepositoryOperationResult.Succeeded("插件包文件已暂存: " + package.PackageId);
        }
    }
}
```

把 `PackageRepositoryService` 中现有 `InstallPackagePayload` 方法体移动到 `CopyPackagePayload`，再把 payload path 解析、`WriteSingleModuleManifest` 和 copy helpers 移入该类。迁移完成后，保留上方参数校验，`CopyPackageToInstallRoot` 调用 `_packageInstallService.InstallPackagePayload(...)`。

- [ ] **步骤 6：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 7：Commit**

```powershell
git add src/PlugHub.Framework/Packages src/PlugHub.StaticValidation/Program.cs
git commit -m "refactor: split package repository service internals"
```

---

### 任务 9：设置配置 store 与日志导出入口

**文件：**
- 创建：`src/PlugHub.Revit2020/Settings/SettingsConfigurationStore.cs`
- 创建：`src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 required 列表加入：

```csharp
"src/PlugHub.Revit2020/Settings/SettingsConfigurationStore.cs",
"src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs",
```

在 settings 验证方法中增加：

```csharp
Require(settingsWindow.Contains("SettingsConfigurationStore"), "FrameworkSettingsWindow must use SettingsConfigurationStore.");
Require(settingsWindow.Contains("ExportLogs"), "FrameworkSettingsWindow must expose log export.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `SettingsConfigurationStore`。

- [ ] **步骤 3：创建 SettingsConfigurationStore**

创建 `SettingsConfigurationStore.cs`：

```csharp
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020.Settings
{
    internal sealed class SettingsConfigurationStore
    {
        public string ConfigDirectory { get; }

        public SettingsConfigurationStore(string configDirectory)
        {
            ConfigDirectory = configDirectory;
        }

        public FrameworkConfiguration Load(FrameworkConfiguration current)
        {
            return current;
        }

        public void Save(FrameworkConfiguration configuration)
        {
        }
    }
}
```

先创建 store 并接入构造函数，随后把 `LoadModuleDocuments`、`TrySave` 中的文件读写逻辑迁入 store。迁移时保持设置页保存后的用户可见行为不变。

- [ ] **步骤 4：创建 ViewModel**

创建 `FrameworkSettingsViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020.Settings
{
    internal sealed class FrameworkSettingsViewModel
    {
        public ObservableCollection<ModuleRow> Modules { get; } = new ObservableCollection<ModuleRow>();
        public ObservableCollection<FeatureRow> Features { get; } = new ObservableCollection<FeatureRow>();
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();
        public ObservableCollection<RepositoryRow> Repositories { get; } = new ObservableCollection<RepositoryRow>();
        public ObservableCollection<RepositoryPackageRow> RepositoryPackages { get; } = new ObservableCollection<RepositoryPackageRow>();
        public ObservableCollection<PendingPackageOperationRow> PendingOperations { get; } = new ObservableCollection<PendingPackageOperationRow>();
        public ObservableCollection<DiagnosticRow> Diagnostics { get; } = new ObservableCollection<DiagnosticRow>();
    }
}
```

把 Window 内部 row collections 替换为 `_viewModel` 属性，DataGrid 绑定 `_viewModel` 的集合。

- [ ] **步骤 5：日志导出入口**

在日志 tab 增加按钮：

```csharp
buttons.Children.Add(CreateButton("导出日志", (sender, args) => ExportLogs()));
```

新增方法：

```csharp
private void ExportLogs()
{
    var targetPath = Path.Combine(BaseDirectory(), "logs", "plughub-logs.zip");
    new PlugHub.Framework.Diagnostics.PlugHubLogExporter().Export(BaseDirectory(), targetPath);
    RefreshStatus("日志已导出: " + targetPath);
}
```

- [ ] **步骤 6：运行验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出 `passed:`。

- [ ] **步骤 7：Commit**

```powershell
git add src/PlugHub.Revit2020/Settings src/PlugHub.Revit2020/FrameworkSettingsWindow.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "refactor: move settings state into dedicated classes"
```

---

### 任务 10：文档、示例入口和最终验证

**文件：**
- 修改：`docs/architecture.md`
- 修改：`docs/development.md`
- 修改：`docs/project-overview.md`
- 修改：`docs/README.md`
- 创建：`docs/plugin-development.md`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `ValidateDocumentationStructure()` 的 required links 增加：

```csharp
"plugin-development.md"
```

新增断言：

```csharp
Require(ReadText("docs/plugin-development.md").Contains("package.json"), "plugin development docs must describe package.json.");
Require(ReadText("docs/plugin-development.md").Contains("IExternalCommand"), "plugin development docs must describe IExternalCommand integration.");
Require(ReadText("docs/development.md").Contains("StagePlugHubOutput=false"), "development docs must document staging opt-out.");
Require(ReadText("docs/architecture.md").Contains("DPAPI"), "architecture docs must document credential protection.");
Require(ReadText("docs/project-overview.md").Contains("V1.2"), "project overview must mention V1.2 architecture hardening.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误消息包含 `plugin-development.md`。

- [ ] **步骤 3：更新文档**

`docs/architecture.md` 增加以下小节：

```markdown
## V1.2 稳定性和安全边界

V1.2 保持 Revit 2020 / net48 主线，不引入 ALC 或 AppDomain 沙箱。框架通过 pending package operations、业务命令异常兜底、文件日志、DPAPI 凭据保护和包兼容字段提高稳定性和可诊断性。
```

`docs/development.md` 增加：

````markdown
### 跳过 dist staging

日常编译可使用：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

当 Revit 正在运行并占用 `dist\Revit2020` 中 DLL 时，使用该参数避免复制输出失败。
````

`docs/project-overview.md` 增加 V1.2 状态说明，删除过期日期表述。

- [ ] **步骤 4：新增插件开发文档**

创建 `docs/plugin-development.md`：

````markdown
# PlugHub 插件开发指南

PlugHub 插件包通过 `package.json` 或 `*.package.json` 声明模块和功能。业务功能在外部 DLL 中实现 Revit `IExternalCommand`，PlugHub 框架只负责发现、展示、启用禁用、排序、日志和命令路由。

## 最小 package.json

```json
{
  "schemaVersion": "1.0",
  "version": "1.0.0",
  "revitVersions": ["2020"],
  "frameworkVersionRange": ">=1.2.0",
  "modules": [
    {
      "id": "hello-world",
      "assembly": "HelloWorld.dll",
      "type": "HelloWorld.Module",
      "displayName": "Hello World",
      "enabled": true,
      "visible": true,
      "features": [
        {
          "id": "hello-world.run",
          "displayName": "Hello",
          "group": "examples",
          "order": 100,
          "defaultState": "Visible",
          "commandAssembly": "HelloWorld.dll",
          "commandType": "HelloWorld.HelloCommand"
        }
      ]
    }
  ]
}
```

## 命令约束

- 命令类型必须实现 Revit `IExternalCommand`。
- 插件不应依赖 PlugHub 框架内部类型。
- Revit 2020 不支持通过 ALC 卸载已加载程序集。
- 更新已加载 DLL 后，仍建议重启 Revit 验收。
````

- [ ] **步骤 5：最终验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

预期：两个命令成功；静态验证输出 `passed:`。

- [ ] **步骤 6：Commit**

```powershell
git add docs src/PlugHub.StaticValidation/Program.cs
git commit -m "docs: document V1.2 hardening workflow"
```

---

## 最终合并前检查

- [ ] 运行完整静态验证：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

- [ ] 运行无 staging 构建：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

- [ ] 运行 release 等价构建：

```powershell
.\scripts\build-revit2020.ps1 -UseRevitApiNuGet -UseRelativeAddinAssembly
```

- [ ] 检查工作区：

```powershell
git status --short --branch
git log --oneline --decorate -10
```

- [ ] 人工检查文档没有声明 Revit 实机测试、Revit 2020 ALC、AppDomain 沙箱或强权限隔离。
