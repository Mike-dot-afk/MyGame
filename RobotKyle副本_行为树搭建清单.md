# RobotKyle (1) 副本 · 行为树改造搭建清单

> 适用对象：Hierarchy 里复制的 **RobotKyle (1)**（原版 RobotKyle 与本清单无关，不会被动任何东西）。
> 原则：**原版预制体、原版所有脚本一律不动**；新增脚本全部放在 `Assets/Scripts/PlayerBehaviorTree/`。
> 时间预估：按本清单操作约 15~25 分钟；之后即可运行测试。

---

## 0. 本方案做了什么（先读懂再动手）

- **新增 25 个文件**（1 个 TPS 副本 + 24 个行为树任务），全部是新增，不修改任何原文件。
- **TPS 副本**：`ThirdPersonShooterController_Tree.cs`，逻辑与原 `ThirdPersonShooterController.cs` 一致，
  多了一个 `treeDriven` 开关：
  - `treeDriven = false`：行为与原脚本完全一致（安全兜底）。
  - `treeDriven = true`：瞄准仍由脚本管；**射击 / 换弹 / 手雷的决策交给行为树**。
- **树挂副本上**，由树决定何时开枪、何时换弹、何时掏雷/蓄力/抛出/取消；移动、重力、相机、IK、子弹物理全留在原脚本体系里。
- 近战（刀）由树触发一次攻击，冷却/连击仍由 Knife 自己管。
- 生命层：死亡/复活仍由 PlayerHealth 管理，树加一个"死亡分支"占位，保证死亡时高优先级打断一切。

---

## 1. 新增文件清单（确认都已存在）

目录：`Assets/Scripts/PlayerBehaviorTree/`

| 文件 | 作用 |
|---|---|
| ThirdPersonShooterController_Tree.cs | TPS 副本（树驱动版），核心 |
| PlayerBT_ActionBase.cs / PlayerBT_ConditionalBase.cs | 两个任务基类（自动找 TPS_Tree 组件） |
| PlayerBT_CanStartGrenade.cs | 条件：按6且有手雷 |
| PlayerBT_StartGrenade.cs | 动作：进入手持手雷 |
| PlayerBT_RMBPressed.cs / PlayerBT_StartCharge.cs | 条件/动作：右键按下进入蓄力 |
| PlayerBT_IsThrowCharging.cs | 条件：蓄力中 |
| PlayerBT_RMBReleased.cs / PlayerBT_LockForThrow.cs | 条件/动作：松右键锁定操作 |
| PlayerBT_LMBCancel.cs / PlayerBT_CancelGrenade.cs | 条件/动作：左键取消 |
| PlayerBT_FinishThrow.cs / PlayerBT_FinishThrowCleanup.cs | 动作：抛出 / 投掷后清理 |
| PlayerBT_IsHoldingGrenade.cs | 条件：手持中 |
| PlayerBT_HasGun.cs / PlayerBT_IsAiming.cs | 条件：持枪 / 瞄准中 |
| PlayerBT_CanReload.cs / PlayerBT_StartReload.cs / PlayerBT_WaitReloadDone.cs | 换弹三件套 |
| PlayerBT_CanFire.cs / PlayerBT_FireWeapon.cs | 射击两件套 |
| PlayerBT_KnifeAttackInput.cs / PlayerBT_KnifeAttack.cs | 近战两件套 |
| PlayerBT_IsDead.cs | 条件：已死亡 |

---

## 2. 准备工作（在 Unity 编辑器里）

### 2.1 保存场景
`Ctrl+S` 保存当前场景（SampleScene 或你实际使用的场景），确认 Hierarchy 里能看到 **RobotKyle (1)**。

### 2.2 打开行为树窗口
菜单 **Tools → Behavior Designer**（若已有窗口则跳过）。

### 2.3 记录原 TPS 参数（关键，别跳过）
在 Hierarchy 选中 **RobotKyle (1)**，在 Inspector 里找到 **ThirdPersonShooterController** 组件：
1. 点组件标题栏右侧的 **齿轮图标 → Copy Component**（复制全部参数，下一步粘贴用）。
2. 用手机拍一张或记下这几个数值（粘贴后核对用）：`Normal Sensitivity`、`Aim Sensitivity`、`Aim Collider Layer Mask`、`Throw Force`、`Is Edit Mode`。

### 2.4 禁用原 TPS 组件（不删除）
取消勾选原 **ThirdPersonShooterController** 组件左上角的启用复选框（灰色即可）。
> 只取消勾选，不点 Remove。这样随时可以重新启用，是回滚的保险。

### 2.5 添加 TPS_Tree 组件并粘贴参数
1. **Add Component**，搜索 `ThirdPersonShooterController_Tree`，添加。
2. 点新组件齿轮 → **Paste Component Values**（把刚才 Copy 的字段值粘贴进来，同名字段自动匹配）。
3. 在 Inspector 里把这个新组件的 **Tree Driven** 复选框 **勾上**。
4. 按 2.3 记录的数值核对一遍：Sensitivity、LayerMask、ThrowForce 等应一致。
5. 检查这些引用字段是否都贴上了（没贴上的手动拖）：`Aim Virtual Camera`、`Crosshair`、`Grenade Prefab`、`Grenade Hand Holder`、`Grenade Trajectory Line`。
6. `Hold Layer Index=1`、`Aiming Layer Index=2`、`Reload Layer Index=3`、`Throw Layer Index=4`（默认值，无需改）。

---

## 3. 添加 Behavior Tree 组件

选中 **RobotKyle (1)**，点 **Add Component** 搜索 `Behavior Tree`，添加后在组件 Inspector 设置：
- **Start When Enabled**：勾上（进场景自动开跑）
- **Restart When Complete**：勾上（树跑完自动重跑，空手时树会频繁完成，必须勾）
- 其余默认。

然后在行为树窗口左上角把当前树切到 RobotKyle (1)（若窗口没自动切过去）。

---

## 4. 搭建行为树（核心，照着拖）

在图区**空白处右键**可以快速搜节点（比如右键 → Search 输入 `PlayerBT_` 或 `Wait`）。按下面的树逐节点添加并连线（父节点底部拖到子节点顶部）。

### 4.1 树结构总览

```
Entry
└── Selector（主决策）                     Abort: Lower Priority
    ├── Sequence「生命」                   Abort: Self
    │   ├── PlayerBT_IsDead（条件）
    │   └── Wait（内置）                   Duration = 999
    ├── Sequence「手雷-进入」              Abort: Self
    │   ├── PlayerBT_CanStartGrenade（条件）
    │   └── PlayerBT_StartGrenade（动作）
    ├── Sequence「手雷-进行」              Abort: Self
    │   ├── PlayerBT_IsHoldingGrenade（条件）
    │   └── Selector                       Abort: Both
    │       ├── Sequence「蓄力入口」        Abort: Self
    │       │   ├── PlayerBT_RMBPressed（条件）
    │       │   └── PlayerBT_StartCharge（动作）
    │       ├── Sequence「蓄力中」          Abort: Self
    │       │   ├── PlayerBT_IsThrowCharging（条件）
    │       │   └── Selector               Abort: Both
    │       │       ├── Sequence「抛出」    Abort: Self
    │       │       │   ├── PlayerBT_RMBReleased（条件）
    │       │       │   ├── PlayerBT_LockForThrow（动作）
    │       │       │   ├── Wait           Duration = 0.75
    │       │       │   ├── PlayerBT_FinishThrow（动作）
    │       │       │   ├── Wait           Duration = 1.25
    │       │       │   └── PlayerBT_FinishThrowCleanup（动作）
    │       │       ├── Sequence「取消」    Abort: Self
    │       │       │   ├── PlayerBT_LMBCancel（条件）
    │       │       │   └── PlayerBT_CancelGrenade（动作）
    │       │       └── Wait               Duration = 0.05（蓄力保持）
    │       └── Wait                       Duration = 0.05（手持等待）
    ├── Sequence「枪械」                   Abort: Self
    │   ├── PlayerBT_HasGun（条件）
    │   ├── PlayerBT_IsAiming（条件）
    │   └── Selector                       Abort: Self
    │       ├── Sequence「换弹」            Abort: Self
    │       │   ├── PlayerBT_CanReload（条件）
    │       │   ├── PlayerBT_StartReload（动作）
    │       │   └── PlayerBT_WaitReloadDone（动作）
    │       ├── Sequence「射击」            Abort: Self
    │       │   ├── PlayerBT_CanFire（条件）
    │       │   └── PlayerBT_FireWeapon（动作）
    │       └── Wait                       Duration = 0.05（持枪待机）
    └── Sequence「近战」                   Abort: Self
        ├── PlayerBT_KnifeAttackInput（条件）
        └── PlayerBT_KnifeAttack（动作）
```

### 4.2 逐节点参数速查

| 节点 | 类型（右键搜索名） | 需要设置的参数 |
|---|---|---|
| 根 Selector | Composite → Selector | Abort Type = **Lower Priority** |
| 生命 / 手雷-进入 / 手雷-进行 / 枪械 / 近战 五个 Sequence | Composite → Sequence | Abort Type = **Self** |
| 手雷-进行 里的 Selector | Composite → Selector | Abort Type = **Both** |
| 蓄力入口 Sequence | Composite → Sequence | Abort Type = **Self** |
| 蓄力中 Sequence | Composite → Sequence | Abort Type = **Self** |
| 蓄力中 里的 Selector | Composite → Selector | Abort Type = **Both** |
| 抛出 / 取消 Sequence | Composite → Sequence | Abort Type = **Self** |
| 换弹 / 射击 Sequence | Composite → Sequence | Abort Type = **Self** |
| 所有 PlayerBT_* 条件/动作 | 直接搜名字 | 无参数（自动找 TPS_Tree） |
| Wait（5 处） | Actions → Wait | Duration：999 / 0.05 / 0.75 / 1.25 / 0.05，按总览填 |

> 提示：条件/动作任务在搜索框里输入 `PlayerBT_` 即可全部列出。

---

## 5. 测试清单（跑场景逐项验证）

1. **空手**：角色正常移动、跳跃、转身（移动由原控制器驱动，树不干扰）。
2. **持枪瞄准**：装备枪后按右键瞄准 → 准星出现、镜头切换、灵敏度变低；松右键恢复。
3. **射击**：瞄准状态按左键 → 开火、弹药减少、后坐力；换弹（R）→ 换弹动画，弹匣补满后可再射。
4. **手雷**：按 6（背包有手雷时）→ 手持手雷；按住右键 → 轨迹线出现、蓄力动画；松右键 → 0.75s 后抛出，1.25s 后动画层复位；蓄力中按左键 → 取消（仍手持，可再蓄力）。
5. **近战**：持刀点击左键 → 挥刀动画 + 伤害；连点触发连击。
6. **死亡**：被敌人打死后 → 黑屏、复活面板；点复活 → 恢复控制。
7. **回滚验证**（可选）：取消勾选 Behavior Tree 组件 + 勾回原 TPS 组件 + 取消勾选 TPS_Tree 的 Tree Driven → 副本行为与原版一致。

---

## 6. 回滚方法（随时可以）

- **只回滚树**：取消勾选 RobotKyle (1) 上的 **Behavior Tree** 组件；勾回原 **ThirdPersonShooterController** 组件；取消勾选 TPS_Tree 的 Tree Driven。
- **彻底清除**：删除 TPS_Tree 组件 + 删除 Behavior Tree 组件（原组件恢复勾选即可）。`Assets/Scripts/PlayerBehaviorTree/` 文件夹整个删掉也不影响原版。

---

## 7. 常见问题

| 现象 | 原因 / 处理 |
|---|---|
| 树不动、节点全灰 | Behavior Tree 组件的 Start When Enabled 没勾；或场景没保存、副本不是当前对象 |
| 空手时树一直"完成-重启" | 正常现象（所有分支条件都不满足），Restart When Complete 已勾就没事 |
| 开枪没反应 | 需要先进入瞄准（IsAiming 条件是枪械分支的门槛）；或弹匣空了 |
| 手雷按6没反应 | 背包里没有名为"手雷"的物品；或已经手持/蓄力中 |
| 手雷抛出后角色卡住 | 检查抛出 Sequence 是否完整走到 FinishThrowCleanup；若中断，重启场景或点复活复位 |
| 换弹中断 | 换弹期间脚本会自动退出瞄准 → 枪械分支暂时失败，这是预期行为；换弹完成后再按瞄准即可 |
| 想恢复原版 | 见第 6 节回滚方法 |
