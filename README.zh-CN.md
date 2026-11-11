# jwmv — Windows 上的 Java / JVM 工具链版本管理器

[English](./README.md)

在 Windows 上安装、管理、切换 Java/JDK 与 JVM 相关工具链版本的轻量工具。基于 .NET 8，灵感来自 [SDKMAN!](https://sdkman.io/) 与 [nvm](https://github.com/nvm-sh/nvm)。

本仓库是在开源项目 [stescobedo92/jwmv](https://github.com/stescobedo92/jwmv) 之上的**二次开发**：保留原有 CLI 能力，并增加 Windows 桌面 GUI、统一的 `jwmv.exe` 宿主、中英文界面、本地 JDK 导入，以及 Inno Setup 安装包。

---

## 本分支相对上游的增强

| 方面 | 说明 |
|------|------|
| **桌面 GUI** | WPF：总览、浏览与安装、已安装、诊断、设置 |
| **统一宿主** | 一个 `jwmv.exe`：双击开 GUI，带参数走 CLI |
| **安装包** | `jwmv-setup-*-win-x64.exe`（`pack.ps1` + Inno Setup） |
| **本地化** | GUI 中文 / 英文（在「设置」中切换） |
| **本地导入** | 注册本机已有 JDK/SDK，不复制文件；若已与 `JAVA_HOME` / `*_HOME` 路径一致则自动记为默认 |
| **安全卸载** | 卸载只移除程序文件与可选 PATH 项，不清理 `JAVA_HOME`、SDK 目录与 `~/.jwmv` 数据 |

上游 CLI 能力（目录安装、校验和、`.jwmvrc`、PowerShell 集成、`doctor` 等）仍然可用。

---

## 功能特性

- **安装 JDK 与 JVM SDK** — Java 来自 [Foojay Disco API](https://api.foojay.io/)，Gradle / Maven / Kotlin 来自官方发布源
- **GUI + CLI** — 图形界面与命令行均可管理
- **即时切换** — 会话级、项目级（`.jwmvrc`）或按候选持久默认
- **导入本地安装** — 指向已有 JDK 目录，无需重新下载
- **下载校验** — 解压前必须匹配目录校验和（可用 `--allow-unverified` 显式跳过）
- **Shell 集成** — PowerShell 配置文件钩子，按项目自动切版本
- **诊断** — `doctor`（CLI 与 GUI）检测 PATH 冲突与配置问题
- **单文件可执行** — 发布产物无需单独安装 .NET 运行时
- **Windows x64** 安装包（若自行发布其他 RID，可参考上游 ARM64 构建方式）

---

## 安装

### Setup.exe（本分支推荐）

1. 构建或获取 `artifacts\jwmv-setup-0.1.0-win-x64.exe`
2. 运行安装程序（默认目录：`%LOCALAPPDATA%\Programs\jwmv`）
3. 可按需将 jwmv 加入用户 `PATH`、创建桌面快捷方式
4. 从开始菜单启动 **jwmv**，或在新终端中执行 `jwmv`

```powershell
# 在本仓库根目录（需 .NET 8 SDK + Inno Setup 6）
.\pack.ps1
# 输出：artifacts\jwmv-setup-0.1.0-win-x64.exe
```

### 便携运行

执行 `.\pack.ps1`（或仅 publish）后也可直接运行：

```text
artifacts\payload\win-x64\jwmv.exe
```

### 上游分发渠道

原项目另有 winget / npm / .NET Tool / GitHub Releases。那些包对应的是**上游**构建，通常不含本分支 GUI。需要桌面界面时，请使用本仓库的 setup.exe。

---

## 快速开始

### 图形界面

1. 打开 **jwmv**
2. **浏览与安装** — 搜索目录并安装 JDK / Gradle / Maven / Kotlin
3. **已安装** — 导入本机 JDK、设为默认、卸载、复制 HOME
4. **诊断** — 检查 `JAVA_HOME`、PATH、Shell 集成
5. **设置** — 切换界面语言（中文 / English）

### 命令行

```powershell
# 查看可用内容
jwmv candidates
jwmv list

# 安装 Java 21（Temurin）并设为默认
jwmv install 21-tem --default

# 导入已有 JDK（注册路径；若已与 JAVA_HOME 一致会自动记为默认）
jwmv import "C:\Program Files\Java\jdk-21" --candidate java

# 验证
jwmv current
java -version

# 安装构建工具
jwmv install gradle 9.5.1
jwmv install maven 3.9.15
jwmv install kotlin 2.3.21

# 仅当前会话切换
jwmv use 17-zulu
jwmv use gradle 9.5.1
```

---

## 界面说明

| 页面 | 用途 |
|------|------|
| **总览** | 当前 SDK 与状态 |
| **浏览与安装** | 目录搜索 / 安装 / 安装后设为默认 |
| **已安装** | 本地列表、导入目录、设为默认、卸载、导出 `use` 脚本 |
| **诊断** | 环境检查 |
| **设置** | 语言等选项 |

忙碌操作会禁用冲突动作；空列表有明确空状态。导入路径若已与 `JAVA_HOME`（或对应 `*_HOME`）一致，会显示「默认」标记，无需再手动点一次「设为默认」。

---

## 命令

### `jwmv candidates [filter]`

列出支持的 SDK 候选。

```powershell
jwmv candidates
jwmv candidates java
```

| 候选 | 来源 |
|------|------|
| `java`   | Foojay Disco API |
| `gradle` | Gradle 版本源 |
| `maven`  | Maven Central 元数据 |
| `kotlin` | JetBrains Kotlin GitHub Releases |

---

### `jwmv list [candidate] [filter]`

列出可安装版本。若第一个参数不是已知候选，则按旧版 Java 过滤器处理。

```powershell
jwmv list java
jwmv list 21
jwmv list gradle
jwmv list maven 3.9
jwmv list kotlin --refresh
```

**别名：** `ls`

| 选项 | 说明 |
|------|------|
| `[candidate]` | 可选候选名 |
| `[filter]` | 可选版本/发行版过滤 |
| `-r, --refresh` | 强制刷新目录缓存 |

---

### `jwmv install [candidate] [version]`

安装 SDK。Java 仍支持旧版简写。

```powershell
jwmv install
jwmv install 21-tem
jwmv install java 21-tem
jwmv install gradle 9.5.1
jwmv install 21.0.4-tem --default
jwmv install 17-zulu --refresh
jwmv install kotlin 1.9.0 --allow-unverified
```

| 选项 | 说明 |
|------|------|
| `[candidate]` | 候选名；Java 简写时可省略 |
| `[version]` | 版本或 Java 标识 |
| `-d, --default` | 安装后设为默认 |
| `-r, --refresh` | 安装前强制刷新目录 |
| `--allow-unverified` | 目录无校验和时仍允许安装 |

**标识格式：** `<version>-<distribution>`

| 别名 | 发行版 |
|------|--------|
| `tem` | Eclipse Temurin |
| `zulu` | Azul Zulu |
| `ms` | Microsoft OpenJDK |
| `graalvm` | GraalVM Community |
| `cor` | Amazon Corretto |
| `lib` | Liberica |
| `sap` | SAP Machine |
| `ojdk` | Oracle OpenJDK |
| `oracle` | Oracle JDK |

---

### `jwmv import <path>`

注册本机已有 SDK 主目录（不复制文件）。

```powershell
jwmv import "D:\jdks\jdk-21" --candidate java
jwmv import "D:\jdks" --candidate java --all
jwmv import "D:\jdks\jdk-21" --default
jwmv import "D:\jre-17" --allow-jre
```

| 选项 | 说明 |
|------|------|
| `<path>` | JDK/SDK 主目录或父目录 |
| `--candidate` | 候选名（默认 `java`） |
| `--alias` | 自定义别名 |
| `-d, --default` | 导入后设为 jwmv 默认 |
| `--all` | 导入路径下发现的全部 home |
| `--allow-jre` | 允许无 `javac` 的 JRE |

若 `JAVA_HOME` / `*_HOME` 已指向该路径，即使未加 `-d` 也会自动记为默认。

---

### `jwmv uninstall [candidate] [version]`

卸载已安装版本（外部导入只删清单，不删原目录）。

```powershell
jwmv uninstall
jwmv uninstall 17-zulu
jwmv uninstall gradle 9.5.1
```

**别名：** `remove`、`delete`、`rm`

---

### `jwmv installed [candidate]`

列出本地已安装版本。

```powershell
jwmv installed
jwmv installed java
```

**别名：** `local`

---

### `jwmv use [candidate] <version>`

仅激活当前 Shell 会话，不改持久默认。

```powershell
jwmv use 17-zulu
jwmv use gradle 9.5.1
jwmv use 21-tem --shell powershell
```

| 选项 | 说明 |
|------|------|
| `[candidate]` | Java 简写时可省略 |
| `<version>` | 要激活的版本 |
| `--shell <SHELL>` | 目标 Shell（默认 powershell） |

---

### `jwmv default [candidate] <version>`

设置新终端的持久默认（更新用户级 `JAVA_HOME` / `*_HOME` 与 `PATH`）。

```powershell
jwmv default 21-tem
jwmv default maven 3.9.15
```

---

### `jwmv current [candidate]`

显示当前生效版本及解析来源（`Default` / `Session` / `Project` / `Environment`）。

```powershell
jwmv current
jwmv current java
```

---

### `jwmv home [candidate] [version]`

打印 SDK 主目录（便于脚本）。

```powershell
jwmv home
jwmv home 17-zulu
jwmv home gradle 9.5.1
$env:JAVA_HOME = $(jwmv home 21-tem)
```

---

### `jwmv upgrade [identifier]`

在同一主版本/厂商轨道上升级到最新补丁。

```powershell
jwmv upgrade 21-tem
jwmv upgrade
```

---

### `jwmv update`

强制刷新本地目录缓存。

```powershell
jwmv update
```

---

### `jwmv doctor`

运行诊断（`JAVA_HOME`、PATH 冲突、PowerShell 集成、`where.exe` 解析）。

```powershell
jwmv doctor
```

---

### `jwmv config`

显示当前配置。

```powershell
jwmv config
```

---

### `jwmv integrate`

安装 PowerShell 配置文件钩子，实现自动切版本。

```powershell
jwmv integrate
jwmv integrate --shell powershell
jwmv integrate --profile "C:\Users\me\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
```

| 选项 | 说明 |
|------|------|
| `--shell <SHELL>` | 目标 Shell |
| `--profile <PATH>` | 自定义 profile 路径 |

---

### `jwmv env`

输出环境激活脚本，或管理项目配置。

```powershell
jwmv env
jwmv env --init
jwmv env init
jwmv env install
jwmv env --cwd ./my-project
```

| 选项 | 说明 |
|------|------|
| `--shell <SHELL>` | 目标 Shell |
| `--cwd <PATH>` | 查找 `.jwmvrc` 的工作目录 |
| `--init` | 输出初始化脚本 |
| `init` | 根据当前 SDK 创建 `.jwmvrc` |
| `install` | 安装 `.jwmvrc` 中缺失的 SDK |

---

### `jwmv flush`

按需清理缓存。

```powershell
jwmv flush --archives
jwmv flush --temp
jwmv flush --catalog
jwmv flush --archives --temp --catalog
```

---

### `jwmv selfupdate`

从 GitHub Releases 自更新（偏向上游签名包）。使用本分支 GUI 构建时，更建议重新安装本仓库的 setup.exe。

```powershell
jwmv selfupdate --check
jwmv selfupdate
```

**别名：** `self-update`

---

## Shell 集成

```powershell
jwmv integrate
```

会在 PowerShell 配置文件（`$PROFILE`）中加入托管块，用于：

1. 读取当前或项目指定的 SDK 版本  
2. 设置 `JAVA_HOME`、`GRADLE_HOME`、`MAVEN_HOME`、`KOTLIN_HOME` 与 `PATH`  
3. 在项目间切换时自动切换版本  

### 手动集成

```powershell
# >>> jwmv initialize >>>
$jwmvInit = & jwmv env --init --shell powershell 2>$null
if ($jwmvInit) { $jwmvInit | Invoke-Expression }
# <<< jwmv initialize <<<
```

---

## 按项目指定 SDK

在项目根目录创建 `.jwmvrc`：

```
java=21-tem
gradle=9.5.1
maven=3.9.15
kotlin=2.3.21
```

仅含 `21-tem` 的旧格式仍会被当作 `java=21-tem`。

每个候选的解析顺序：

1. **Session** — `jwmv use`  
2. **Project** — `.jwmvrc`（向上查找）  
3. **Default** — `jwmv default`  
4. **Environment** — 未配置 jwmv 默认时，沿用已有 `JAVA_HOME` / `*_HOME`  

---

## 配置

数据目录：`~/.jwmv/`

```
~/.jwmv/
├── config.json
├── candidates/
│   ├── java/
│   ├── gradle/
│   ├── maven/
│   └── kotlin/
├── archives/
├── tmp/
└── var/
    ├── catalog-v2.json
    └── manifests/
```

### `config.json`（示例）

```json
{
  "preferredDistributionAlias": "tem",
  "catalogRefreshHours": 6,
  "autoEnvEnabled": true,
  "defaultShell": "powershell",
  "defaultJavaAlias": "21-tem",
  "defaultVersions": {
    "java": "21-tem",
    "gradle": "9.5.1"
  },
  "selfUpdateRepository": "stescobedo92/jwmv"
}
```

| 键 | 默认 | 说明 |
|----|------|------|
| `preferredDistributionAlias` | `"tem"` | 未指定时的默认发行版 |
| `catalogRefreshHours` | `6` | 目录自动刷新间隔（小时） |
| `autoEnvEnabled` | `true` | 是否启用 `.jwmvrc` 自动切换 |
| `defaultShell` | `"powershell"` | 脚本生成目标 Shell |
| `defaultJavaAlias` | — | 旧版 Java 默认别名 |
| `defaultVersions` | `{}` | 各候选持久默认 |
| `selfUpdateRepository` | — | 自更新用的 GitHub `owner/repo` |

---

## 从源码构建

依赖：

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php)（生成安装包）

```powershell
# 运行测试
dotnet test

# 发布并生成 setup.exe
.\pack.ps1
.\pack.ps1 -Version 0.1.0 -Configuration Release
```

产物：

| 路径 | 说明 |
|------|------|
| `artifacts\payload\win-x64\jwmv.exe` | GUI + CLI 统一宿主 |
| `artifacts\jwmv-setup-0.1.0-win-x64.exe` | Windows 安装包 |

---

## 使用示例

```powershell
cd ~\examples\legacy-app
@"
java=17-cor
maven=3.9.15
"@ > .jwmvrc

cd ~\examples\modern-api
@"
java=21-tem
gradle=9.5.1
"@ > .jwmvrc

jwmv install 17-cor
jwmv install 21-tem --default
jwmv install maven 3.9.15
jwmv install gradle 9.5.1

# 启用 Shell 集成后，进入目录会自动切换
cd ~\examples\legacy-app
java -version   # → Corretto 17

cd ~\examples\modern-api
java -version   # → Temurin 21
```

---

## 安全说明

- **下载校验** — 默认失败即拒绝；仅在显式 `--allow-unverified` 时跳过。
- **环境变量** — `jwmv default` 修改用户 `Path` 时保留 `%VAR%` 形式；只删除由 jwmv 自己写入的 `JAVA_HOME` / `*_HOME`。
- **程序卸载** — Inno 卸载程序**不会**清理 SDK 主目录变量或 `~/.jwmv` 数据。
- **Shell 集成** — 配置文件钩子只应用提供方定义的变量名与转义后的值。
- 上游还提供 Sigstore 签名发布与 CodeQL 等说明，详见[原项目](https://github.com/stescobedo92/jwmv)与（如有）[SECURITY.md](./SECURITY.md)。

---

## 架构

```
Jwmv.Gui             → WPF 桌面界面（视图 / ViewModel / 本地化）
Jwmv.Cli             → Spectre.Console CLI 与宿主入口
Jwmv.Core            → 接口、模型、工具（无外部依赖）
Jwmv.Infrastructure  → 目录提供者、存储、Windows 环境集成
Jwmv.Tests           → xUnit 测试
installer/           → Inno Setup 脚本
pack.ps1             → 发布并编译 setup.exe
```

采用整洁架构与依赖倒置；服务以接口形式通过 `Microsoft.Extensions.DependencyInjection` 注入。

---

## 上游与致谢

- 上游项目：[stescobedo92/jwmv](https://github.com/stescobedo92/jwmv)
- 本分支在其基础上扩展 GUI 打包与 Windows 桌面工作流，并尽量保持 CLI 兼容。

---

## 许可证

MIT License — 见 [LICENSE](./LICENSE)。原有代码部分仍适用上游版权声明。
