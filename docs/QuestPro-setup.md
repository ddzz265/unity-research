# SereneNight_Test：Quest Pro 修改记录

更新日期：2026-11-10。原项目：`D:\unityprogramme\SereneNight_Test`。

## 已完成

- 在 Android 播放器中启用头部追踪；保留原场景眼高，头部和左右手柄使用同一追踪空间，路线仍移动原来的父物体。
- Quest 控制器使用 Input System 的瞄准位置/旋转、追踪状态和扳机输入，接入现有 XR Interaction Toolkit 的 UI 点击和滑条拖动。失去追踪时释放输入并隐藏射线。
- 保留每个场景现有 XRUIInputModule 和世界空间问卷。正念、暴露、音乐三个构建场景均检查到 headPose、leftRay、rightRay 和 trackingOrigin 引用。没有 XR 配置的模板 SampleScene 已从构建列表禁用。
- Android 射线距离从原来的 8 米扩展到 60 米，覆盖暴露场景 50 米长桥上的任务目标。
- Android 色彩处理保持启用状态，避免切换虚化时调色跳变；景深与所需深度纹理仅在 C/F 虚化时启用（保留原相机已有的深度需求）。C/F 组背景以 3 秒渐变进入 Gaussian 景深。问卷与手柄射线由清晰 UI 相机绘制；没有问卷和追踪手柄时移除该叠加相机。
- Mobile URP：HDR 关闭，MSAA 改为 2x，阴影距离 50 米改为 25 米。渲染比例由 0.8 改为 1，以改善文字和边缘采样；同时调整植被预算。这会减少远处阴影和 HDR 后处理表现，需要在真机确认画质可接受，尤其是用于实验条件比较时。
- 添加 OpenXR 1.16.1、XR Management 4.6.1 包依赖，以及 `Serene Night > Configure Quest Pro (Android)` 配置菜单。
- 保留 D/E/F 的真实吸气触发，没有添加手柄模拟吸气。当前仍需提供传感器型号、连接方式和数据协议，再接入现有 `NotifyInhalation()`；在此之前这些组会停留在等待吸气阶段。

详细变更及验证边界见 [2026-11-09 更新说明](2026-11-09-update.md)。仅 E 组启用沉降反馈，位移为 9 个 UI 单位；其他组恢复普通选中反馈。面板出现位置与歪头倾斜已校正，正念相机运动模糊已关闭。

## 在另一台电脑打包

1. 复制修改后的整个项目，至少包含 Assets、Packages、ProjectSettings。不要只复制场景；不要复制 Library 缓存覆盖另一台电脑的缓存。
2. 使用 Unity 6000.5.10f1，安装 Android Build Support、SDK/NDK 和 OpenJDK。打开项目，让 Package Manager 解析新依赖。
3. 执行 `Serene Night > Configure Quest Pro (Android)`。该菜单保存 Android OpenXR Loader、自动初始化、Meta Quest Support、Touch Pro/Touch 控制器配置、Single Pass Instanced、ARM64、IL2CPP、Vulkan、Linear 色彩空间、最低 Android API 32 和优化帧节奏。Quest Pro 的设备清单标识为 `cambria`。
4. 在 Build Profiles 切换 Android，并运行 XR Plug-in Management 的 Project Validation。确认 Android 默认质量为 Mobile、目标设备包含 Quest Pro。
5. 按实验需要选择起始场景：Serene_Night_Experiment、Serene_Night_Exposure 或 Serene_Night_Music。当前默认起始场景仍是正念实验场景；构建列表本身不会自动依次播放所有场景。
6. 在打包电脑构建 APK。新增构建检查会拦截缺少 Android OpenXR / Quest / Touch Pro 配置的构建，并提示执行上述菜单。

此电脑未安装 Android 模块；按用户要求，没有启动 Unity、安装 Android 模块或构建 APK。配置菜单的实际执行与 Unity 完整导入验证留给打包电脑，因此当前不声称 Android 平台配置、Shader 编译和完整构建已经验证通过。

## 已验证与未验证

已使用项目现有 Unity 程序集和 Unity 随附 Roslyn 编译器，对整个运行时代码分别执行桌面条件及 `UNITY_ANDROID`（去掉 Editor/Standalone 条件）编译，两次均返回 0。Android 检查仍引用现有编辑器程序集，这能检出本次修改的 C# / Unity API 问题，但不能代替 Android 平台编译、IL2CPP 或设备执行。编辑器构建脚本也已使用现有 Unity 程序集通过源码编译检查；仍未执行 Unity Android 完整导入、Gradle/IL2CPP 构建或真机验证。三个实验场景的引用检查通过。

没有连接并读取实际头显的设备信息；Quest Pro 是用户确认的目标设备。程序启动会记录 `XR device model` 供真机核对。没有原始 APK、Profiler 录制或真机帧率，因此尚不能给出“卡顿已彻底解决”的结论。项目核心体验是三维场景加音频和移动路线，未在实验运行时代码中发现 VideoPlayer；本次没有把问题误判为视频解码卡顿。

## Quest Pro 验收

- 查看设备型号日志和 OpenXR 启动日志，确认运行于 Quest Pro，视图为立体且转头追踪正确，眼高与路线正确。
- 三个场景分别测试左手、右手扳机选择；问卷单选、多选、滑条拖动、上一题、下一题、提交及结果保存。
- 暴露场景测试远处任务目标、暂停/继续、结束体验；摘戴头显、控制器失去追踪再恢复后，不应留下按住状态。
- 各 A–F 组检查显示时序、淡入/展开效果；C/F 背景模糊且问卷文字清晰。D/E/F 需要真实传感器事件才能继续，禁止把手柄点击当作真实吸气数据。
- 使用相同场景、路线、画质和刷新率比较修改前后 CPU/GPU 帧时间、掉帧和温度；播放阶段与问卷阶段分别记录。72 Hz 的帧预算约 13.89 ms，90 Hz 约 11.11 ms；这些只是预算，不是实测结果。不要把 `Application.targetFrameRate` 当成 XR 头显刷新率的保证。
- 若森林仍明显卡顿，下一步应根据 GPU/CPU 数据定位植被、透明材质、阴影或几何开销，再决定裁剪距离/LOD；本轮已减少音乐场景植被并优化暴露场景透明材质；这些视觉取舍和 E 组沉降幅度需要记录为固定实验版本。

## 备份与参考

`OriginalFiles` 保存被修改文件的原始版本。回退时先关闭 Unity，再还原对应文件，同时移除本次新增的 `Assets/SereneNightExperiment/Editor/QuestBuildSettings.cs` 及其 `.meta`。在另一台电脑执行配置菜单后会新增 XR 设置资产，回退时应另外检查它们。

参考开源方案：[Unity 官方 XRI 示例的 UI 交互](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/UI-2D.md)、[Unity 官方 Meta OpenXR 示例](https://github.com/Unity-Technologies/mr-example-meta-openxr)。本次复用项目现有 XRI，未整体导入新示例项目。

优化依据：[Unity 独立式 XR 的 URP 优化建议](https://docs.unity3d.com/Manual/xr-untethered-device-optimization.html)。具体配置 API 对照官方 Unity Registry 下载的 OpenXR 1.16.1 与 XR Management 4.6.1 源码。


## 2026-11-10：问卷、转头延迟与同步记录

- 三个实验场景共享 17 道问卷题，所有题目均为 1–7 分；Q1、Q2、C1 的端点说明已相应调整。
- Android Quest 构建时启用 Late Latching；正念播放时标记追踪头部供渲染前姿态修正使用，不添加头部平滑或改变路线。此项不代表已证明转头卡顿的唯一根因，也不是零掉帧保证。
- 眼动 CSV 格式化移至后台，保留原来的 41 列；新增呼吸、校时与会话文件，通过同前缀关联。
- 电脑中转程序位于 `Tools/PhysiologySync`；真实呼吸带的厂商接口尚待型号确定后接入。记录链路不调用 `NotifyInhalation()`，D/E/F 仍需要经过验证的真实吸气事件。
- Android 清单新增局域网通信所需的 INTERNET 权限。没有呼吸数据时仅记录状态，不写入模拟样本。

详细协议、使用方法及数据有效性限制见 [同步记录说明](../Tools/PhysiologySync/README.md)；本轮验证情况见 [更新记录](2026-11-10-update.md)。修改后需要重新构建 APK，已安装的旧 APK 不会自动更新。
