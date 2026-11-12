# SleepPrompt（进入睡眠）

一个 Windows 上的「一键让电脑睡眠」确认小工具：弹出深色玻璃拟态卡片，确认后进入 3 秒倒计时，倒计时结束让电脑睡眠。Esc / 取消可随时中断。

C# / WPF 单文件源码构建，无 XAML、无第三方依赖，编译产物就是一个 exe。

## 下载

最新版本见 [GitHub Releases](https://github.com/Summer-Ever/SleepPrompt/releases/tag/v1.2)。下载 `SleepPrompt-v1.2.exe` 后双击即可运行，无需安装；运行要求为 Windows 10/11 自带的 .NET Framework 4.x。

未签名的网络下载文件可能触发 SmartScreen 提示，选择「更多信息 → 仍要运行」即可。

## 使用

- **Enter** 开始或立即执行，**Esc** 取消；窗口可拖动、始终置顶、不显示在任务栏。
- 确认后月亮图标淡出，圆环走完 3 秒倒计时，然后按所选模式执行电源动作。
- 默认进入 **S0 Modern Standby**（关屏后由系统自动进入低功耗空闲），不是传统 S3 待机。

| 参数 | 作用 |
| --- | --- |
| `--mode standby` | 默认。广播 `WM_SYSCOMMAND / SC_MONITORPOWER` 关屏，进入 S0 |
| `--mode hibernate` | 明确进入 S4 休眠（`SetSuspendState(true, …)`） |
| `--mode legacy` | 保留 v1.0 行为 `SetSuspendState(false, false, false)`，仅作对照 |
| `--dry` / `/dry` | 走到触发点但不执行电源动作 |
| `--simulate` | 走完整睡前流程（停动画 → Hide → 后台线程 → Close → Shutdown），但不发任何电源动作 |
| `--log <路径>` | 记录 `LOADED` / `COUNTDOWN_START` / `SLEEP_TRIGGERED` / `DONE` / `CANCEL` 等 |
| `--shot <路径>` | 启动后自动把窗口渲染成 PNG 并退出 |
| `--shotcount` | 配合 `--shot`，截取倒计时状态的画面 |

## 从源码构建

只需 Windows 自带的 .NET Framework 4.x 编译器，无需 Visual Studio 或 SDK。

```bat
build.bat
```

`install-to-desktop.bat` 会在此基础上完成「编译 → 备份桌面旧文件 → 覆盖桌面 → 打印 SHA-256 校验」。两个脚本在失败时都会保持桌面原文件不动。

手动编译的等价命令：

```bat
set "FR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPFREF=%FR%\WPF"
"%FR%\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:SleepPrompt.exe /win32icon:app.ico /win32manifest:app.manifest ^
  /lib:%FR%,%WPFREF% ^
  /r:System.dll /r:System.Xaml.dll ^
  /r:"%WPFREF%\WindowsBase.dll" ^
  /r:"%WPFREF%\PresentationCore.dll" ^
  /r:"%WPFREF%\PresentationFramework.dll" ^
  SleepApp.cs
```

> 若机器未安装 .NET Framework Reference Assemblies，WPF 程序集只存在于运行时目录的 `WPF\` 子目录，`csc` 默认不会搜索该目录，需按上面用 `/lib:` 加搜索路径并用完整路径写 `/r:`，否则会报 `CS0006`。`SleepApp.csproj` 供装了 VS / MSBuild 的环境使用。

编译产物 `*.exe` 与本地备份按 `.gitignore` 约定不入库。

## 测试

```bat
python test_flow.py
```

覆盖 Enter 开始、Esc 取消、倒计时到底三条路径。用例 4 使用 `--simulate`，验证窗口能自行撤下、进程干净退出且**不会真的让电脑睡眠**；脚本会先检查 exe 是否带有 v1.2 标记，旧版会跳过该用例以免误触发电源动作。

## 目录结构

| 文件 | 说明 |
| --- | --- |
| `SleepApp.cs` | 主源码，C# / WPF，纯代码构建 UI |
| `app.manifest` | asInvoker 权限、PerMonitorV2 DPI 感知 |
| `app.ico` | 程序图标（月亮渐变） |
| `mkicon.py` | 用 Pillow 生成 ico 与预览图 |
| `build.bat` | 零依赖编译脚本 |
| `install-to-desktop.bat` | 编译并覆盖桌面文件 |
| `SleepApp.csproj` | Visual Studio / MSBuild 工程文件 |
| `test_flow.py` | 自动化测试 |
| `shot.py` / `shot2.py` | 窗口截图辅助 |
| `*.png` | 界面截图与设计稿 |
| `old/睡眠确认.ps1` | 早期版本：WinForms + PowerShell 简易确认框 |

## 实现要点

- UI 全部由 C# 代码搭建，无 XAML，因此单文件 `csc` 即可编译；只用 C# 3.0 级语法。
- 全局缩放系数 `K`，改一处即可整体缩放界面；配色集中在 `AccentA` / `AccentB`。
- 视觉：极光径向渐变 + 玻璃描边卡片 + DropShadow + 淡入位移动画 + 图标呼吸光晕；月亮由两圆 `GeometryCombineMode.Exclude` 相减得到。
- **S0 Modern Standby 不是可通过 API 请求的状态**。在只有 S0 / S4 的机器上，`SetSuspendState(false, …)` 请求的 S3 会被电源管理器降级成 S4 休眠；正确做法是关屏，让系统自行进入低功耗空闲。
- 所有阻塞式电源调用都放在后台线程，且窗口在所有电源动作之前隐藏。

## 版本历史

- **v1.2** — 修复唤醒后倒计时窗口残留旧画面、以及 UI 线程被电源广播阻塞导致的无响应。此前 `Close()` 排在关屏广播之后，而关屏会立刻触发待机冻结进程，窗口销毁来不及提交，DWM 恢复的便是旧帧；`HWND_BROADCAST` 同步广播在 UI 线程上还会被无响应窗口拖住。现在先停动画并 `Hide()`，把 `SendMessageTimeout` / `SetSuspendState` 移入 `ThreadPool`，广播超时缩短到 300ms，改用 `OnExplicitShutdown` 显式退出，并新增 `--simulate`。
- **v1.1** — 修复在仅支持 S0 / S4 的机器上「睡眠」被降级成 S4 休眠的问题，改为关屏进入 S0。
- **v1.0** — 初始版本。
