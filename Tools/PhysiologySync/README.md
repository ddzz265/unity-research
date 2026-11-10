# Quest Pro 眼动与呼吸同步记录

工程：`D:\unityprogramme\SereneNight_Test`。方案：真实呼吸带 → 电脑厂商适配程序 → 局域网 UDP → Quest Pro。Quest Pro 眼动仍使用原来的 OpenXR 合并视线。

## 现在完成什么、还缺什么

已实现电脑中转、头显接收、软件校时、两路记录关联、状态与异常记录、电脑原始数据备份及离线配对工具。三个实验场景、A–F 组均沿用现有眼动记录器自动启动这条链路。

**尚未适配任何具体呼吸带。** 用户目前没有呼吸带，无法验证设备连接、采样率、真实幅值、单位、设备延迟及实际同步精度。后续必须确认型号及其原始呼吸波形接口，再实现厂商适配程序。心率胸带、心率数值和 RR 心搏间期不能自动当作真实呼吸波形。本次没有生成模拟呼吸数据的产品功能。

没有呼吸信号时，呼吸文件只有状态行，不会写入虚构的样本或以零值代替缺失。眼动可以独立继续记录。没有头显时，电脑程序仍可保留接收到的真实呼吸样本，但不能宣称已经与眼动同步。

## 使用方法

1. 在安装对应 Android Build Support 的电脑，用修改后的工程重新构建并安装 Quest Pro APK。本机尚不能完整构建 Android APK，旧 APK 不含此功能。
2. 电脑与 Quest Pro 接入同一可信局域网，确认两者可以相互访问。头显会监听 UDP 5006；电脑端本地适配入口为 `127.0.0.1:5005`。不需要使用已经禁用的呼吸球。
3. 在电脑上使用 Python 3.10 或更新版本启动：

   ```powershell
   python "D:\unityprogramme\SereneNight_Test\Tools\PhysiologySync\breath_bridge.py" --quest 192.168.1.50
   ```

   将示例 IP 换成头显的局域网 IPv4 地址。这个示例地址不是已经检测到的设备地址。Python 仅使用标准库，不需要安装额外包。
4. 启动头显中的实验，允许眼动权限，并开启头显眼动追踪。电脑程序会显示 `Quest session: ...`。这只表示软件握手成功，不表示呼吸带已连接或眼动有效。
5. 呼吸带的厂商适配程序向电脑本机 UDP 5005 发送真实数据。当前没有此设备适配，直接运行中转程序会等待数据，不会生成呼吸波形。
6. 记录完成后正常退出头显应用；电脑中转程序按 Ctrl+C 正常停止。不要依靠强制终止来保存最后一秒的缓冲数据。

如有防火墙提示，只允许这次实验所需的本地程序及可信局域网通信。本协议用于实验局域网，未提供互联网身份认证或加密，不要把端口映射到公网。

## 文件与字段

头显文件保存在 `Application.persistentDataPath/EyeTracking`，编辑器预览则在 `D:\Codexput\SereneNightResponses\EyeTracking`。同一次记录共用相同的文件名前缀作为 `session_id`：

| 文件 | 内容 |
| --- | --- |
| `<session_id>.csv` | 原有眼动 41 列；列序和含义保持不变 |
| `<session_id>_respiration.csv` | 真实呼吸样本及状态行 |
| `<session_id>_clock_sync.csv` | 每次校时的四个时间戳、偏移与往返时间 |
| `<session_id>_session.json` | 文件关联、时钟定义、错误、样本计数与队列丢行统计 |

电脑原始样本与握手日志默认保存到 `D:\Codexput\SereneNightResponses\BreathBridge` 下的 JSONL 文件。即使头显临时不可达，电脑收到的样本仍会保存在此；程序不会把断连期间的旧数据悄悄当作实时信号重放。`udp_sent=true` 只表示 UDP 发送成功，不表示头显已经接收。

呼吸 CSV 的 `kind=sample` 才是样本；`kind=state` 是状态变化，没有幅值。常见状态：

| 状态 | 含义 |
| --- | --- |
| `waiting_for_belt` | 尚未收到呼吸样本；不等同于设备已连接 |
| `bridge_connected` | 电脑程序已握手 |
| `receiving` | 最近 1.5 秒内收到新的序号样本；不是信号质量认证 |
| `signal_lost` | 超过 1.5 秒没有新样本 |
| `app_inactive` | 头显应用失去焦点或暂停 |
| `listener_unavailable` / `network_error` | 接收端口或网络错误 |
| `stopped` | 已请求正常结束 |

重要字段：

- `source_pc_seconds`：电脑单调时钟上的样本时间。
- `receive_app_seconds`：头显接收时间，映射到 Unity 应用时间。
- `app_seconds`：经过软件校时的电脑样本时间；校时不足或过期时留空。
- `device_seconds`：可选原始设备时间，原样保留；尚未自动映射设备时钟。
- `time_basis=pc_receive`：只能确定电脑收到数据的时刻，仍包含呼吸带到电脑的延迟。
- `time_basis=pc_acquisition`：设备适配程序已经把采样时刻映射到同一电脑的 `time.perf_counter()` 时钟；只能在确实实现该映射后使用。
- `sync_valid=1`：软件时钟映射可用，**不是物理同步精度或传感器有效性的认证**。
- `rtt_seconds`：选中校时点的网络往返时间，不是总传感器延迟，也不是已证明的误差上界。
- `sequence_gap_observed`：发现的序号空缺；迟到数据可能补回，不应直接等同于最终丢包数。
- `sequence_state=out_of_order_or_duplicate`：乱序或重复包；保留原记录，默认离线配对不使用它。
- `queue_dropped_rows`：保存队列来不及处理而丢掉的行数。队列有界，不能保证任意设备采样率下均不丢行。
- `*_at_receive`：接收时最近一个 Unity 帧的实验上下文，不是设备真实采样瞬间的上下文。需要采样时刻的阶段信息时，使用同时间轴的眼动帧配对。

组编号为 A=0、B=1、C=2、D=3、E=4、F=5；阶段编号沿用 `ExperimentPhase`。题号沿用程序当前索引定义。

## 呼吸带适配入口

厂商适配程序每收到一个真实呼吸样本，发送一个 UTF-8 JSON 对象到电脑本机 UDP 5005，必须明确声明 `kind=respiration`。格式模板如下，尖括号内容需要替换为真实值，模板本身不能作为数据发送：

```text
{"kind":"respiration","value":<真实数值>,"unit":"<真实单位或raw>","device_seconds":<可选设备时间>}
```

`value` 必须为有限数值，`unit` 为不超过 32 个字符的字母、数字或 `_.:/-`。`device_seconds` 可以省略。不能发送 NaN、Infinity、心率代替值，或呼吸球动画的大小。

默认在电脑收到包时打 `pc_receive` 时间戳。只有适配器确认采样时刻已转换为该电脑的 `time.perf_counter()` 时钟时，才额外提供 `pc_sample_seconds` 和 `time_basis="pc_acquisition"`。设备启动秒数、Unix 时间和 `DateTime.Now` 不能直接填入 `pc_sample_seconds`。

呼吸带 SDK 的连接、校准、采样时间转换、断连和批量数据展开需要根据具体型号实现。记录模块不推断吸气/呼气，不自动调用 `NotifyInhalation`，避免擅自改变实验组行为。

## 时间同步及离线配对

电脑与头显约每秒进行一次四时间戳交换。头显保留最近 30 秒的校时点，选择最小往返时间对应的偏移；至少成功交换 3 次后才填写校正时间，10 秒没有新校时就停止标为同步。保存原始校时点便于后续检查或重建时钟映射。

眼动 `app_seconds` 是 Unity 帧读取时刻；`input_update_seconds` 是输入设备更新时间，**不是已经确认的眼动传感器原始采样时刻**。呼吸的设备到电脑延迟、眼动到应用延迟及网络不对称仍需真机和真实设备测量。这里完成的是软件记录时间轴对齐，不能宣称硬件同步或毫秒级精度。

将同前缀的四个头显文件放在电脑同一文件夹后，可生成配对表：

```powershell
python "D:\unityprogramme\SereneNight_Test\Tools\PhysiologySync\align_recordings.py" "D:\Codexput\数据\<session_id>.csv" "D:\Codexput\数据\配对结果.csv" --max-delta 0.1
```

这是路径模板，需要替换实际文件名。脚本按校正时间寻找最近的呼吸样本；眼动无效、呼吸未同步、应用暂停或超过指定配对距离时留空，不插值、不补零。默认 0.1 秒只是可调整的配对阈值，**不是同步精度承诺**。同一呼吸样本可能对应多个眼动帧，保留序号用于识别，不能把这些行当作独立的新呼吸采样。

## 验证范围

运行时代码的桌面、Android 非 Editor 分支及编辑器构建钩子通过源码编译检查。使用独立 .NET 接收端和实际 Python 中转程序进行了本机回环网络测试，覆盖输入格式、无设备、未校时、校时失效、数据顺序与空缺、暂停、断连恢复、端口占用、文件落盘和离线配对；详见交付包中的 `software-tests.log`。

测试中的人工数值只保存在 `D:\Codexput\2026-11-09\unity-project-2-3-6\work\physiology-test`，不是实验数据。本次没有连接 Quest Pro 或真实呼吸带，没有测量头显帧时间，没有完整构建 APK。因此不能保证真机零掉帧、指定采样率无丢包或真实设备同步精度。

本次保留问卷、音频、场景、动线、选项反馈和实验组切换方式；未引入原生 SDK 或第三方包。接收和保存运行于后台线程，主线程仅复制基本实验上下文字段，但后台分配和设备采样负载仍需后续真机验证。

## 开源与官方参考

先检查了成熟的 [LSL4Unity](https://github.com/labstreaminglayer/LSL4Unity) 与 [LSL 时间同步说明](https://labstreaminglayer.readthedocs.io/info/time_synchronization.html)。本实现参考其“保留原始时间戳与校时测量”的原则，沿用当前工程已有 UDP 技术，不是 LSL 协议，也不直接与 LabRecorder 互通。Android 构建钩子补充了局域网通信需要的 [INTERNET 权限](https://developer.android.com/develop/connectivity/network-ops/connecting)。

修改前备份位于交付包中的 `Before-fix`，部署文件位于交付包中的 `Patch`。本次尚未同步 GitHub。
