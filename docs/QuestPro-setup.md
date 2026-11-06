# SereneNight_Test：Quest Pro 修改记录

修改日期：2026-11-06。原项目：`D:\unityprogramme\SereneNight_Test`。

## 已完成

- 在 Android 播放器中启用头部追踪；保留原场景眼高，头部和左右手柄使用同一追踪空间，路线仍移动原来的父物体。
- Quest 控制器使用 Input System 的瞄准位置/旋转、追踪状态和扳机输入，接入现有 XR Interaction Toolkit 的 UI 点击和滑条拖动。失去追踪时释放输入并隐藏射线。
- 保留每个场景现有 XRUIInputModule 和世界空间问卷。正念、暴露、音乐三个构建场景均检查到 headPose、leftRay、rightRay 和 trackingOrigin 引用。没有 XR 配置的模板 SampleScene 已从构建列表禁用。
- Android 射线距离从原来的 8 米扩展到 60 米，覆盖暴露场景 50 米长桥上的任务目标。
- Android 播放阶段不再由问卷组件强制维持后处理/深度纹理。C/F 组显示问卷时才启用原 Gaussian 景深；其他组不添加模糊。UI 相机关闭独立深度/颜色复制及后处理抗锯齿。
- Mobile URP：HDR 关闭，MSAA 改为 2x，阴影距离 50 米改为 25 米。原本 0.8 的渲染比例保持原值。这会减少远处阴影和 HDR 后处理表现，需要在真机确认画质可接受，尤其是用于实验条件比较时。
- 添加 OpenXR 1.16.1、XR Management 4.4.0 包依赖，以及 `Serene Night > Configure Quest Pro (Android)` 配置菜单。
- 保留 D/E/F 的真实吸气触发，没有添加手柄模拟吸气。当前仍需提供传感器型号、连接方式和数据协议，再接入现有 `NotifyInhalation()`；在此之前这些组会停留在等待吸气阶段。

## 在另一台电脑打包

1. 复制修改后的整个项目，至少包含 Assets、Packages、ProjectSettings。不要只复制场景；不要复制 Library 缓存覆盖另一台电脑的缓存。
2. 使用 Unity 6000.5.10f1，安装 Android Build Support、SDK/NDK 和 OpenJDK。打开项目，让 Package Manager 解析新依赖。
3. 执行 `Serene Night > Configure Quest Pro (Android)`。该菜单保存 Android OpenXR Loader、自动初始化、Meta Quest Support、Touch Pro/Touch 控制器配置、Single Pass Instanced、ARM64、IL2CPP、Vulkan、Linear 色彩空间、最低 Android API 32 和优化帧节奏。Quest Pro 的设备清单标识为 `cambria`。
4. 在 Build Profiles 切换 Android，并运行 XR Plug-in Management 的 Project Validation。确认 Android 默认质量为 Mobile、目标设备包含 Quest Pro。
5. 按实验需要选择起始场景：Serene_Night_Experiment、Serene_Night_Exposure 或 Serene_Night_Music。当前默认起始场景仍是正念实验场景；构建列表本身不会自动依次播放所有场景。
6. 在打包电脑构建 APK。新增构建检查会拦截缺少 Android OpenXR / Quest / Touch Pro 配置的构建，并提示执行上述菜单。

此电脑未安装 Android 模块；按用户要求，没有启动 Unity、安装 Android 模块或构建 APK。配置菜单的实际执行与 Unity 完整导入验证留给打包电脑，因此当前不声称 Android Loader 设置资产已经生成。

## 已验证与未验证

已使用项目现有 Unity 程序集和 Unity 随附 Roslyn 编译器，对整个运行时代码分别执行桌面条件及 `UNITY_ANDROID`（去掉 Editor/Standalone 条件）编译，两次均返回 0。Android 检查仍引用现有编辑器程序集，这能检出本次修改的 C# / Unity API 问题，但不能代替 Android 平台编译、IL2CPP 或设备执行。新编辑器配置脚本 API 已对照下载的官方包源码检查，尚未经过 Unity 包导入后的完整编译。三个实验场景的引用检查通过。

没有连接并读取实际头显的设备信息；Quest Pro 是用户确认的目标设备。程序启动会记录 `XR device model` 供真机核对。没有原始 APK、Profiler 录制或真机帧率，因此尚不能给出“卡顿已彻底解决”的结论。项目核心体验是三维场景加音频和移动路线，未在实验运行时代码中发现 VideoPlayer；本次没有把问题误判为视频解码卡顿。

## Quest Pro 验收

- 查看设备型号日志和 OpenXR 启动日志，确认运行于 Quest Pro，视图为立体且转头追踪正确，眼高与路线正确。
- 三个场景分别测试左手、右手扳机选择；问卷单选、多选、滑条拖动、上一题、下一题、提交及结果保存。
- 暴露场景测试远处任务目标、暂停/继续、结束体验；摘戴头显、控制器失去追踪再恢复后，不应留下按住状态。
- 各 A–F 组检查显示时序、淡入/展开效果；C/F 背景模糊且问卷文字清晰。D/E/F 需要真实传感器事件才能继续，禁止把手柄点击当作真实吸气数据。
- 使用相同场景、路线、画质和刷新率比较修改前后 CPU/GPU 帧时间、掉帧和温度；播放阶段与问卷阶段分别记录。72 Hz 的帧预算约 13.89 ms，90 Hz 约 11.11 ms；这些只是预算，不是实测结果。不要把 `Application.targetFrameRate` 当成 XR 头显刷新率的保证。
- 若森林仍明显卡顿，下一步应根据 GPU/CPU 数据定位植被、透明材质、阴影或几何开销，再决定裁剪距离/LOD；本次没有批量删除场景对象或修改实验时长来掩盖问题。

## 备份与参考

`OriginalFiles` 保存被修改文件的原始版本。回退时先关闭 Unity，再还原对应文件，同时移除本次新增的 `Assets/SereneNightExperiment/Editor/QuestBuildSettings.cs` 及其 `.meta`。在另一台电脑执行配置菜单后会新增 XR 设置资产，回退时应另外检查它们。

参考开源方案：[Unity 官方 XRI 示例的 UI 交互](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/UI-2D.md)、[Unity 官方 Meta OpenXR 示例](https://github.com/Unity-Technologies/mr-example-meta-openxr)。本次复用项目现有 XRI，未整体导入新示例项目。

优化依据：[Unity 独立式 XR 的 URP 优化建议](https://docs.unity3d.com/Manual/xr-untethered-device-optimization.html)。具体配置 API 对照官方 Unity Registry 下载的 OpenXR 1.16.1 与 XR Management 4.4.0 源码。
