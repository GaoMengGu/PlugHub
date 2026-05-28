# DLL 签名方案

## 结论

PlugHub 可以用免费方式做 DLL 签名，但要区分用途：

- 本地开发、内部测试：可以使用 self-signed code-signing certificate，成本为 0。缺点是默认不被其他机器信任，只适合开发或受控内网。
- 公开分发：免费且公开可信的选择很少。开源项目可以评估 SignPath Foundation 这类托管签名服务；是否通过取决于项目和服务方审核。
- 商业公开分发：通常仍需要购买公开受信任的代码签名证书，或使用云签名服务。

## 本地开发签名

先构建：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

创建 self-signed 开发证书并签名：

```powershell
.\scripts\sign-revit2020.ps1 -CreateSelfSignedDevCertificate
```

使用已有证书 Thumbprint 签名：

```powershell
.\scripts\sign-revit2020.ps1 -Thumbprint "<Thumbprint>"
```

使用 PFX 文件签名：

```powershell
.\scripts\sign-revit2020.ps1 -CertificatePath "D:\certs\plughub.pfx" -CertificatePassword "<password>"
```

脚本使用 `signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256`，并只签名 `dist\Revit2020` 下的 `PlugHub*.dll`。

## 免费公开签名评估

SignPath Foundation 面向符合条件的开源项目提供免费代码签名流程。该方案通常需要：

- 项目源码公开。
- CI 产生可追踪的构建产物。
- 按服务方要求配置仓库、项目和签名策略。

这类方案适合后续将 PlugHub 和 `PlugHub_Modules` 做成稳定开源发布流程时再接入。当前阶段建议先保留 `scripts\sign-revit2020.ps1`，本地开发用 self-signed，公开发布前再决定证书或托管签名服务。

## GitHub Release 中的 cosign 签名

仓库包含 `.github/workflows/release.yml`。该 workflow 只在推送 `V*` 版本 tag 时运行，例如：

```powershell
git tag V1.0.0
git push origin V1.0.0
```

workflow 使用 GitHub OIDC 和 cosign keyless signing，对发布产物做 Sigstore blob 签名：

- `PlugHub*.dll`：生成对应的 `.sigstore.json` bundle。
- `PlugHub-Revit2020-<tag>.zip`：生成 zip 包和 zip 的 `.sigstore.json` bundle。

Revit API 引用通过 NuGet 仅用于 CI 编译。release workflow 使用 `.\scripts\build-revit2020.ps1 -UseRevitApiNuGet`，通过 `Autodesk.Revit.SDK` 包获得编译期引用，不需要把 `RevitAPI.dll` / `RevitAPIUI.dll` 放入仓库，也不需要配置包含 Autodesk DLL 的 GitHub secret。

本地构建和 Revit 实机验收仍使用真实 Revit 安装目录中的 API DLL。NuGet 包只解决 CI 编译引用问题，不代表可以绕过 Revit 运行环境，也不改变 Autodesk DLL 的分发边界。

cosign 签名是发布校验签名，不是 Windows Authenticode 内嵌签名。它不能消除 Windows 或 Revit 的“未知发布者”提示；如果目标是消除该提示，仍需 Authenticode 代码签名证书。

## 约束

- 不要把 PFX、私钥、密码或 token 提交到仓库。
- self-signed 证书不能消除 Windows 或 Revit 对未知发布者的公开信任问题，除非目标机器信任该证书。
- 签名应在构建完成后执行；二进制文件被修改后需要重新签名。
