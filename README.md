# TxCommand

[English](#english) | [中文](#中文说明)

---

# English

## 1. Overview

`TxCommand` is a C# class-library project for **Siemens Tecnomatix Process Simulate**. It calculates joint-space PTP (Point-to-Point) motion between two robot Via locations in a welding operation and creates intermediate robot path locations at a fixed time interval.

The project addresses the following tasks:

- Read robot joint poses at the start and end locations from Process Simulate.
- Calculate PTP duration from joint displacement, maximum speed, and maximum acceleration.
- Synchronize robot axes J1–J6 and a servo-gun external axis to one common motion duration.
- Interpolate in joint space using a `0.01 s` time step.
- Convert interpolated joint poses to TCP poses and write them back as `TxRoboticViaLocationOperation` objects.
- Select a PTP timing strategy based on the robot's per-axis maximum-speed configuration.

This project is a Process Simulate customization example/tool library. It is not a standalone robot controller and does not command a physical robot.

## 2. Features

| Feature | Description |
|---|---|
| Joint-pose retrieval | Uses `TxRobot.GetPoseAtLocation` to obtain joint values at Via locations. |
| PTP duration calculation | Calculates triangular or trapezoidal velocity-profile duration from displacement, speed, and acceleration limits. |
| Six-axis synchronization | Uses one shared `ptpTime` so all robot axes start and finish together. |
| Servo-gun synchronization | Compares servo-gun duration with robot duration; the longer duration becomes the final motion time. |
| Path interpolation | Generates intermediate joint positions every `0.01 s`, updates the robot pose, and creates Via locations. |
| Adaptive timing | `TxRobotptpInterpolationCal` selects the original or limiting-axis-priority strategy based on J1–J6 maximum speeds. |
| Console diagnostics | Prints start/end joints, joint deltas, speed limits, robot type, PTP duration, and axis peak speeds. |

## 3. Project Layout

| File | Purpose |
|---|---|
| `TxRobotptpPathCal.cs` | Original PTP interpolator. It calculates robot PTP time from the axis with the largest absolute joint displacement. |
| `TxRobotptpPathCalforKawasaki.cs` | Kawasaki-style variant. It gives J2 priority when J2 reaches its speed limit, unless another axis needs more time. |
| `TxRobotptpInterpolationCal.cs` | Recommended adaptive interpolator. It selects a strategy from the six-axis maximum-speed configuration. |
| `TxItemsDimensionalVec.cs` | Exports 2D/3D approximation points from the selected component/device/gun/gripper to a CSV file on the desktop. |
| `Class1.cs` | Basic placeholder class in the project. |
| `motionparameters-e.pdf` | Motion-parameter reference material included with the local workspace, when available. |

## 4. Requirements and Dependencies

### Required environment

- Windows
- Visual Studio; this repository uses the classic .NET Framework project format
- .NET Framework 4.7.2
- Siemens Tecnomatix Process Simulate / eMPower development environment
- Access to `Tecnomatix.Engineering.dll` and `Tecnomatix.Engineering.Ui.dll`

The project currently references these Tecnomatix DLL paths:

```text
C:\Program Files\Tecnomatix_2402\eMPower\Tecnomatix.Engineering.dll
C:\Program Files\Tecnomatix_2402\eMPower\Tecnomatix.Engineering.Ui.dll
```

If the installed Tecnomatix version or directory differs, update the related `HintPath` entries in `TxCommand.csproj`.

### COM dependencies

The project also declares the following COM references:

- `Microsoft.Office.Core`
- `Microsoft.Office.Interop.Excel`
- `ROBOTICMOPENGINELib`
- `VBIDE`

Whether they are needed depends on the local Process Simulate/Office installation and the intended workflow. If a build fails, verify that the required components are installed and registered.

## 5. Quick Start

1. Clone the repository on a computer with Process Simulate/eMPower APIs installed.
2. Open `TxCommand.sln` in Visual Studio.
3. Verify that the Tecnomatix DLL `HintPath` entries in `TxCommand.csproj` match the local installation.
4. Build the solution.
5. In Process Simulate, select a `TxWeldOperation`.
6. Ensure that the operation has at least two direct `TxRoboticViaLocationOperation` children:
   - first Via location: start point;
   - second Via location: end point.
7. Invoke the desired class from the Process Simulate command/plugin entry point, for example `new TxRobotptpInterpolationCal()`.

The class inserts intermediate Via locations between the start and end locations. The original end location remains in place, so the interpolation loop creates only intermediate points and does not duplicate the end point.

> Note: the classes depend on the active Process Simulate document and active selection. The constructor returns without action if no `TxWeldOperation` is selected, fewer than two Via locations exist, or required robot/gun data is unavailable.

## 6. PTP Motion Model

### 6.1 Joint displacement

For each robot joint:

\[
\Delta q_i = q_{end,i} - q_{start,i}
\]

The current implementation processes J1–J6, represented by array indexes `0` through `5`.

### 6.2 Triangular velocity profile

For short moves where the axis must decelerate before reaching its speed limit:

\[
v_{peak} = \sqrt{a\lvert\Delta q\rvert}
\]

\[
T = 2\sqrt{\frac{\lvert\Delta q\rvert}{a}}
\]

Where \(a\) is maximum acceleration, \(\Delta q\) is joint displacement, \(v_{peak}\) is reachable peak speed, and \(T\) is the minimum rest-to-rest motion time.

### 6.3 Trapezoidal velocity profile

For moves long enough to reach the speed limit, the code uses an acceleration–constant-speed–deceleration profile:

\[
T = \frac{v_{max}}{a} + \frac{\lvert\Delta q\rvert}{v_{max}}
\]

Where \(v_{max}\) is the axis maximum speed.

### 6.4 Final synchronized time

A robot timing strategy first determines the robot duration. That duration is then compared with the servo-gun duration:

\[
T_{final}=\max(T_{robot}, T_{gun})
\]

The final `ptpTime` is passed to every robot-axis and gun interpolation function. An axis that could finish sooner reduces its effective peak speed so it finishes synchronously.

## 7. `TxRobotptpInterpolationCal` in Detail

`TxRobotptpInterpolationCal` is the main adaptive interpolator in this repository. It preserves the path generation, three-stage position interpolation, gun synchronization, and Process Simulate API usage of `TxRobotptpPathCal`; it changes only the way the robot PTP time is selected.

### 7.1 Robot-type classification

After acquiring the robot, the class compares `MaxSpeed` across J1–J6:

- If all six maximum speeds are exactly equal, it writes:

  ```text
  Robot type: same maximum speed for J1-J6
  ```

- If one or more speeds differ, it writes:

  ```text
  Robot type: different maximum speeds for J1-J6; lowest maximum speed axis: Jn
  ```

`Jn` is the first axis having the lowest maximum speed and is intended as a diagnostic hint.

### 7.2 Equal maximum speeds: original strategy

When all J1–J6 `MaxSpeed` values are equal, `calculatePTPtime` remains consistent with `TxRobotptpPathCal`:

1. Find the axis with the largest absolute joint displacement.
2. Use its maximum speed and maximum acceleration to calculate PTP duration.
3. Interpolate all axes against that shared duration.

This preserves the behavior of the original class for robot models with equal speed limits.

### 7.3 Different maximum speeds: limiting-axis-priority strategy

When maximum speeds differ, the class uses the same core idea as `TxRobotptpPathCalforKawasaki`, but it does not hard-code J2:

1. Find the group of axes having the **lowest maximum speed** among J1–J6.
2. From that group, select axes that can reach their own `MaxSpeed` in the current move.
3. If several candidate axes reach the limit, select the one with the longest individual minimum duration as the priority axis.
4. Calculate the priority axis's trapezoidal-profile duration.
5. Calculate individual minimum durations for the remaining five axes.
6. If the longest remaining-axis duration is not greater than the priority-axis duration, the priority axis determines robot PTP time.
7. If another axis needs more time, that longest other-axis duration determines robot PTP time.
8. If no lowest-maximum-speed axis reaches its own limit in the current move, fall back to the original largest-joint-displacement strategy.

For example, if J2 and J3 have the same lowest maximum speed:

- If J2 does not reach its speed limit but J3 does, J3 is the priority axis.
- If both J2 and J3 reach their limits, the one with the longer individual duration is selected.
- If any other axis still requires a longer minimum time, that other axis determines the final robot duration.

### 7.4 Interpolation flow

After final `ptpTime` is calculated, the class:

1. Compares it with servo-gun duration and chooses the final synchronized duration.
2. Computes the number of interpolation steps using `(int)(ptpTime / 0.01)`.
3. Calls `calCurrentRobotPosedata` for each time sample.
4. Calls `calCurrentRobotAixJointData` for every robot axis.
5. Calculates the current displacement in acceleration, constant-speed, or deceleration phase.
6. Assigns joint values to `robot.CurrentPose` and reads absolute TCP translation and RPY rotation.
7. Creates a new `TxRoboticViaLocationOperation` through `addrobotPathViaLoc`.
8. Inserts the new Via location before the original end location and sets `RRS_MOTION_TYPE = 1`.

The robot axes and servo gun use the same final `ptpTime`; therefore, they finish synchronously in the calculation model.

## 8. Main Methods

| Method | Description |
|---|---|
| `calculateStartPointPosture` | Gets `TxPoseData` for a robot Via location. |
| `calculateJointsChange` | Calculates J1–J6 displacement between start and end locations. |
| `calculatePTPtime` | Calculates robot PTP time; the adaptive class includes speed-type detection and limiting-axis-priority logic. |
| `calculateServoPTPtime` | Calculates the minimum duration for the servo-gun driving joint. |
| `calculateJointsactualSpeed` | Back-calculates actual peak speeds for all six robot axes for a shared duration. |
| `calCurrentRobotPosedata` | Calculates the complete robot joint pose at one interpolation time. |
| `calCurrentRobotAixJointData` | Calculates one robot axis position in the acceleration, constant-speed, or deceleration phase. |
| `calCurrentServoGunJointData` | Calculates servo-gun external-axis position at one time sample. |
| `addrobotPathViaLoc` | Creates a robot Via location from TCP pose and gun opening value. |

## 9. Current Limitations and Precautions

- The implementation is fixed to a six-axis robot and loops from `0` to `5`.
- It assumes that a welding operation, robot, servo gun, and `RobotExternalAxesData[0]` are present and valid.
- It uses only the first two direct Via locations under the welding operation as start and end.
- Equal maximum-speed detection uses direct `double` equality. Tiny floating-point differences can classify a robot as having different maximum speeds.
- The time step is fixed at `0.01 s`; a smaller step produces more Via locations and may increase Process Simulate document load.
- `div` uses truncation, so the last intermediate point may not occur exactly at `ptpTime`; the original end location represents the final position.
- The velocity profile is a simplified, symmetric model based on maximum speed and acceleration. It does not include jerk, controller look-ahead, blending, singularity handling, collision checking, or controller-specific motion planning.
- Save the Process Simulate document and validate results on a copy before generating production paths.

## 10. Open-Source Publication Recommendations

Before publishing, consider the following:

1. Add a license file, such as `MIT`, `Apache-2.0`, or a license required by your organization.
2. Remove models, process data, local paths, and other confidential material that should not be public.
3. Clarify that Tecnomatix, Process Simulate, and their APIs are Siemens products/interfaces and users must have appropriate licenses.
4. Ask issue reporters to provide Process Simulate version, robot model, speed/acceleration settings, and reproducible steps.
5. Add repeatable unit tests or offline mathematical validation cases for the timing logic.

## 11. Disclaimer

This project is provided “as is” for offline simulation, research, and Process Simulate customization reference. Users are responsible for validating generated paths, joint limits, reachability, collision safety, and controller compatibility. Do not treat the calculated result as sufficient proof that a physical robot can operate safely.


---

# 中文说明

## 1. 项目简介

`TxCommand` 是一个面向 **Siemens Tecnomatix Process Simulate** 的 C# 类库项目，用于在机器人焊接操作（`TxWeldOperation`）中的两个机器人 Via 点之间计算关节空间 PTP（Point-to-Point）运动，并按固定时间间隔生成中间机器人路径点。

项目重点解决以下问题：

- 从 Process Simulate 中读取起点和终点的机器人关节姿态；
- 根据各机器人关节的最大速度、最大加速度和关节变化量计算 PTP 总时间；
- 使机器人 J1–J6 与伺服焊钳外部轴在同一总时间内同步完成运动；
- 按 `0.01 s` 时间步长进行关节空间插补；
- 将插补结果转换为 TCP 位姿并作为新的 `TxRoboticViaLocationOperation` 写回焊接操作；
- 针对不同机器人最大速度配置，选择不同的 PTP 时间判定策略。

该项目是一个 Process Simulate 二次开发示例/工具库，不是独立的机器人控制器，也不会向真实机器人下发运动指令。

## 2. 核心功能

| 功能 | 说明 |
|---|---|
| 关节姿态读取 | 通过 `TxRobot.GetPoseAtLocation` 获取 Via 点对应的关节值。 |
| PTP 时间计算 | 根据关节位移、最大速度和最大加速度，计算三角形或梯形速度曲线所需时间。 |
| 六轴同步 | 使用一个统一的 `ptpTime`，让全部机器人轴同时起动、同时结束。 |
| 焊钳同步 | 将伺服焊钳驱动关节的时间加入比较；若焊钳更慢，则焊钳时间成为最终总时间。 |
| 路径插补 | 以 `0.01 s` 为间隔计算中间关节位置、更新机器人姿态并创建 Via 点。 |
| 自适应选时 | `TxRobotptpInterpolationCal` 根据 J1–J6 的最大速度是否相同，选择原始或限速轴优先的时间规则。 |
| 控制台诊断 | 输出起终点关节角、关节变化量、最大速度、机器人类型、PTP 时间和各轴峰值速度。 |

## 3. 工程结构

| 文件 | 作用 |
|---|---|
| `TxRobotptpPathCal.cs` | 原始 PTP 插补实现。以关节变化量绝对值最大的轴计算机器人 PTP 时间。 |
| `TxRobotptpPathCalforKawasaki.cs` | Kawasaki 风格的变体。优先考察 J2；J2 本次能达到最大速度时，由 J2 或耗时更长的其他轴决定时间。 |
| `TxRobotptpInterpolationCal.cs` | 推荐的自适应 PTP 插补实现。根据六轴最大速度配置自动选择计算策略。 |
| `TxItemsDimensionalVec.cs` | 从当前选中的组件/设备/焊钳/夹具读取二维或三维几何近似点，并导出 CSV 到桌面。 |
| `Class1.cs` | 项目中的基础占位类。 |
| `motionparameters-e.pdf` | 项目中随附的运动参数参考资料（如存在于本地工作区）。 |

## 4. 环境与依赖

### 必需环境

- Windows
- Visual Studio（本工程使用传统 .NET Framework 项目格式）
- .NET Framework 4.7.2
- Siemens Tecnomatix Process Simulate / eMPower 开发环境
- 可访问 `Tecnomatix.Engineering.dll` 与 `Tecnomatix.Engineering.Ui.dll`

项目文件当前引用的 Tecnomatix DLL 路径为：

```text
C:\Program Files\Tecnomatix_2402\eMPower\Tecnomatix.Engineering.dll
C:\Program Files\Tecnomatix_2402\eMPower\Tecnomatix.Engineering.Ui.dll
```

如果本机安装目录、Tecnomatix 版本或 eMPower 路径不同，请在 `TxCommand.csproj` 中调整对应 `HintPath`。

### COM 依赖

工程还声明了以下 COM 引用：

- `Microsoft.Office.Core`
- `Microsoft.Office.Interop.Excel`
- `ROBOTICMOPENGINELib`
- `VBIDE`

是否需要它们取决于本地 Process Simulate/Office 安装与实际使用场景。构建失败时，请先检查相应组件是否已注册。

## 5. 快速开始

1. 在已安装 Process Simulate/eMPower API 的计算机上克隆仓库。
2. 使用 Visual Studio 打开 `TxCommand.sln`。
3. 检查 `TxCommand.csproj` 中 Tecnomatix DLL 的 `HintPath` 是否与本机安装路径一致。
4. 生成项目。
5. 在 Process Simulate 中选择一个 `TxWeldOperation`。
6. 确保该焊接操作下至少有两个直接子级 `TxRoboticViaLocationOperation`：
   - 第一个 Via 点：起点；
   - 第二个 Via 点：终点。
7. 从 Process Simulate 的命令/插件入口调用所需类的构造函数，例如 `new TxRobotptpInterpolationCal()`。

执行后，类会在起点与终点之间插入新的 Via 点。原始终点 Via 点保留，因此插补循环只创建中间点，而不会重复创建终点。

> 注意：这些类依赖当前 Process Simulate 文档和当前选择集。没有选择 `TxWeldOperation`、目标操作少于两个 Via 点，或缺少所需机器人/焊钳数据时，构造函数会直接返回。

## 6. PTP 运动模型

### 6.1 关节变化量

对于每个机器人关节：

\[
\Delta q_i = q_{end,i} - q_{start,i}
\]

当前实现处理 J1–J6，即数组索引 `0` 到 `5`。

### 6.2 三角形速度曲线

当位移较短，关节来不及加速到速度上限就需要减速时，采用三角形速度曲线：

\[
v_{peak} = \sqrt{a\lvert\Delta q\rvert}
\]

\[
T = 2\sqrt{\frac{\lvert\Delta q\rvert}{a}}
\]

其中：

- \(a\)：该轴最大加速度；
- \(\Delta q\)：该轴关节变化量；
- \(v_{peak}\)：该次运动可达到的峰值速度；
- \(T\)：该轴从静止到静止的最短时间。

### 6.3 梯形速度曲线

当位移足够大且关节可以达到速度上限时，采用加速—匀速—减速的梯形速度曲线：

\[
T = \frac{v_{max}}{a} + \frac{\lvert\Delta q\rvert}{v_{max}}
\]

其中 \(v_{max}\) 为该轴最大速度。

### 6.4 最终同步时间

机器人时间先由各计算策略确定；随后与焊钳时间比较：

\[
T_{final}=\max(T_{robot}, T_{gun})
\]

最终的 `ptpTime` 被传给所有机器人轴和焊钳的插补函数。若某个轴本身可更快完成，它会根据统一总时间降低实际峰值速度，以实现同步结束。

## 7. `TxRobotptpInterpolationCal` 详解

`TxRobotptpInterpolationCal` 是本工程中用于适配不同六轴最大速度配置的主要类。它保留 `TxRobotptpPathCal` 的路径生成、三段式位置插补、焊钳同步和 Process Simulate API 调用方式，只调整机器人 PTP 时间的选取规则。

### 7.1 机器人类型判定

在读取到机器人后，类会比较 J1–J6 的 `MaxSpeed`：

- 如果六个轴的最大速度完全相同，控制台输出：

  ```text
  Robot type: same maximum speed for J1-J6
  ```

- 如果存在不同速度，控制台输出：

  ```text
  Robot type: different maximum speeds for J1-J6; lowest maximum speed axis: Jn
  ```

这里的 `Jn` 是最大速度最小轴中的第一个轴号，用于诊断提示。

### 7.2 六轴最大速度相同：原始策略

如果 J1–J6 的 `MaxSpeed` 完全相同，`calculatePTPtime` 与 `TxRobotptpPathCal` 保持一致：

1. 找到绝对关节变化量最大的轴；
2. 使用该轴的最大速度和最大加速度计算 PTP 时间；
3. 后续所有轴按照该总时间同步插补。

此策略适用于六轴速度配置一致的机器人模型，并保持与原始类相同的行为。

### 7.3 六轴最大速度不同：限速轴优先策略

当六轴最大速度不同，类采用与 `TxRobotptpPathCalforKawasaki` 相同的基本思想，但不固定使用 J2：

1. 找出 J1–J6 中**最大速度最小**的一组轴；
2. 在该组轴中筛选本次运动能够达到自身 `MaxSpeed` 的轴；
3. 如果有多个候选轴达到上限，选择其单轴最短时间较长的轴作为优先轴；
4. 计算优先轴的梯形速度曲线时间；
5. 计算其余五个轴的单轴最短时间；
6. 如果其余轴的最长时间不超过优先轴时间，则优先轴时间决定机器人 PTP 时间；
7. 如果其他轴更慢，则由其他轴的最长时间决定机器人 PTP 时间；
8. 如果所有最大速度最小轴在本次运动中都无法达到自身最大速度，则回退至原始“关节变化量最大轴”策略。

这样可以避免把“第一个最小速度轴”固定为控制轴。例如 J2、J3 的最大速度相同且都是最低值：

- 如果 J2 未达到最大速度、J3 达到最大速度，则优先采用 J3 的时间；
- 如果 J2 和 J3 都达到最大速度，则采用两者中单轴时间较长的轴；
- 如果其他任一轴需要更长的最短运动时间，则该其他轴仍决定最终机器人时间。

### 7.4 插补过程

最终 `ptpTime` 计算完成后，类执行以下步骤：

1. 将 `ptpTime` 与伺服焊钳时间比较，取得最终同步时间；
2. 使用 `0.01 s` 步长计算插补次数：`(int)(ptpTime / 0.01)`；
3. 每个时间点调用 `calCurrentRobotPosedata`；
4. 每个机器人轴调用 `calCurrentRobotAixJointData`；
5. 根据加速段、匀速段、减速段计算该轴当前位移；
6. 将关节姿态赋给 `robot.CurrentPose`，读取 TCP 的绝对位置与 RPY 姿态；
7. 使用 `addrobotPathViaLoc` 创建新的 `TxRoboticViaLocationOperation`；
8. 将新的 Via 点插入原终点之前，并设置 `RRS_MOTION_TYPE = 1`。

机器人轴和伺服焊钳都使用相同的最终 `ptpTime`，所以它们在计算模型中同步结束。

## 8. 主要方法说明

| 方法 | 说明 |
|---|---|
| `calculateStartPointPosture` | 获取指定机器人 Via 点的 `TxPoseData`。 |
| `calculateJointsChange` | 计算起终点 J1–J6 的关节变化量。 |
| `calculatePTPtime` | 计算机器人 PTP 时间；在自适应类中包含速度类型判断和限速轴优先逻辑。 |
| `calculateServoPTPtime` | 计算伺服焊钳驱动关节的最短时间。 |
| `calculateJointsactualSpeed` | 在给定统一时间下反算六个机器人轴的实际峰值速度。 |
| `calCurrentRobotPosedata` | 计算某一插补时刻的完整机器人关节姿态。 |
| `calCurrentRobotAixJointData` | 计算某个机器人轴在加速、匀速或减速阶段的位置。 |
| `calCurrentServoGunJointData` | 计算伺服焊钳外部轴在某一时刻的位置。 |
| `addrobotPathViaLoc` | 根据 TCP 位姿和焊钳开度创建新的机器人 Via 点。 |

## 9. 当前限制与使用注意事项

- 当前实现固定按六轴机器人处理，循环范围为 `0..5`。
- 当前逻辑假定焊接操作、机器人、伺服焊钳和 `RobotExternalAxesData[0]` 均存在且有效。
- 当前逻辑只使用焊接操作下的前两个直接 Via 点作为起点和终点。
- 最大速度相同的判断使用 `double` 直接相等比较；模型中存在极小浮点误差时，可能被归类为“不同最大速度”。
- 时间步长固定为 `0.01 s`；更小步长会生成更多 Via 点并增加 Process Simulate 文档负担。
- `div` 使用向下取整，因此最后一个中间点不一定正好位于 `ptpTime`；原始终点 Via 点承担最终到达位置。
- 速度曲线是基于最大速度和最大加速度的简化对称模型，不包含 jerk（加加速度）、控制器前瞻、拐角过渡、奇异点处理、碰撞检查或真实控制器专有运动规划。
- 生成路径前建议保存 Process Simulate 文档，并先在副本上验证结果。

## 10. 开源使用建议

在公开发布前，建议补充以下内容：

1. 选择并提交许可证文件，例如 `MIT`、`Apache-2.0` 或适合团队要求的许可证；
2. 删除不适合公开的模型、工艺数据、公司路径和机密文件；
3. 明确 Tecnomatix、Process Simulate 及其 API 属于 Siemens 的产品和接口，使用者需要自行具备合法授权；
4. 在 Issue 模板中要求提交者说明 Process Simulate 版本、机器人型号、最大速度/加速度参数和最小复现步骤；
5. 对关键时间计算增加可重复的单元测试或离线数学验证用例。

## 11. 免责声明

本工程按“现状”提供，仅用于 Process Simulate 中的离线仿真、研究和二次开发参考。使用者应自行验证生成路径、关节限制、可达性、碰撞安全性和控制器兼容性。请勿将本工程的计算结果直接视为真实机器人安全运行的充分条件。

