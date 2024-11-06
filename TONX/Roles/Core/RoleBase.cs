using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Core;

public abstract class RoleBase : IDisposable
{
    public PlayerControl Player { get; private set; }
    /// <summary>
    /// 玩家状态
    /// </summary>
    public readonly PlayerState MyState;
    /// <summary>
    /// 玩家任务状态
    /// </summary>
    public readonly TaskState MyTaskState;
    /// <summary>
    /// 是否拥有任务
    /// 默认只有在您是船员的时候有任务
    /// </summary>
    protected Func<HasTask> hasTasks;
    /// <summary>
    /// 是否拥有任务
    /// </summary>
    public HasTask HasTasks => hasTasks.Invoke();
    /// <summary>
    /// 任务是否完成
    /// </summary>
    public bool IsTaskFinished => MyTaskState.IsTaskFinished;
    /// <summary>
    /// 可以成为叛徒
    /// </summary>
    public bool CanBeMadmate { get; private set; }
    /// <summary>
    /// 是否拥有技能按钮
    /// </summary>
    public bool HasAbility { get; private set; }
    public RoleBase(
        SimpleRoleInfo roleInfo,
        PlayerControl player,
        Func<HasTask> hasTasks = null,
        bool? hasAbility = null,
        bool? canBeMadmate = null
    )
    {
        Player = player;
        this.hasTasks = hasTasks ?? (roleInfo.CustomRoleType == CustomRoleTypes.Crewmate ? () => HasTask.True : () => HasTask.False);
        CanBeMadmate = canBeMadmate ?? Player.Is(CustomRoleTypes.Crewmate);
        HasAbility = hasAbility ?? roleInfo.BaseRoleType.Invoke() is
            RoleTypes.Shapeshifter or
             RoleTypes.Phantom or
            RoleTypes.Engineer or
            RoleTypes.Scientist or
            RoleTypes.Tracker or
            RoleTypes.GuardianAngel or
            RoleTypes.CrewmateGhost or
            RoleTypes.ImpostorGhost;

        MyState = PlayerState.GetByPlayerId(player.PlayerId);
        MyTaskState = MyState.GetTaskState();

        CustomRoleManager.AllActiveRoles.Add(Player.PlayerId, this);
    }
#pragma warning disable CA1816
    public void Dispose()
    {
        OnDestroy();
        CustomRoleManager.AllActiveRoles.Remove(Player.PlayerId);
        Player = null;
    }
#pragma warning restore CA1816
    public bool Is(PlayerControl player)
    {
        return player.PlayerId == Player.PlayerId;
    }
    /// <summary>
    /// 创建实例后立刻调用的函数
    /// </summary>
    public virtual void Add()
    { }
    /// <summary>
    /// 实例被销毁时调用的函数
    /// </summary>
    public virtual void OnDestroy()
    { }
    /// <summary>
    /// RoleBase 专用 RPC 发送类
    /// 会自动发送 PlayerId
    /// </summary>
    protected class RoleRPCSender : IDisposable
    {
        public MessageWriter Writer;
        public RoleRPCSender(RoleBase role)
        {
            Writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.CustomRoleSync, SendOption.Reliable, -1);
            Writer.Write(role.Player.PlayerId);
        }
        public void Dispose()
        {
            AmongUsClient.Instance.FinishRpcImmediately(Writer);
        }
    }
    /// <summary>
    /// 创建一个待发送的 RPC
    /// PlayerId 是自动添加的，所以您可以忽略
    /// </summary>
    /// <param name="rpcType">发送的 RPC 类型</param>
    /// <returns>用于发送的 RoleRPCSender</returns>
    protected RoleRPCSender CreateSender()
    {
        return new RoleRPCSender(this);
    }
    /// <summary>
    /// 接受到 RPC 时的函数
    /// 任何职业收到任何类型的 RPC 时都会调用，所以您需要判断是否是您需要的 RPC 类型
    /// RoleRPCSender 中的 PlayerId 在传递前会被删除，所以您不需要知道它
    /// </summary>
    /// <param name="reader">收到 RPC 内容</param>
    /// <param name="rpcType">收到 RPC 类型</param>
    public virtual void ReceiveRPC(MessageReader reader)
    { }
    /// <summary>
    /// 可以使用技能按钮
    /// </summary>
    /// <returns>true：可以使用能力按钮</returns>
    public virtual bool CanUseAbilityButton() => true;
    /// <summary>
    /// 在 BuildGameOptions 中调用的函数
    /// </summary>
    public virtual void ApplyGameOptions(IGameOptions opt)
    { }

    /// <summary>
    /// CheckMurder 作为目标处理函数<br/>
    /// 该函数用于您被击杀前的检查，调用该函数前已经调用过 OnCheckMurderAsKiller 函数<br/>
    /// 因此您不需要判断击杀者是否真的尝试击杀你，击杀者对您尝试的击杀是确定的<br/>
    /// 对于无法被击杀的状态（无敌、被保护）等，设置 info.CanKill = false<br/>
    /// 若本次击杀本身就不合法，您可以返回 false 以完全终止本次击杀事件<br/>
    /// </summary>
    /// <param name="info">击杀事件的信息</param>
    /// <returns>false：不再向下执行击杀事件</returns>
    public virtual bool OnCheckMurderAsTarget(MurderInfo info) => true;

    /// <summary>
    /// 已确定本次击杀会发生，但在真正发生前，您想要做点什么吗？
    /// </summary>
    /// <param name="info">击杀事件的信息</param>
    public virtual void BeforeMurderPlayerAsTarget(MurderInfo info)
    { }

    /// <summary>
    /// MurderPlayer 作为目标处理函数
    /// </summary>
    /// <param name="info">击杀事件的信息</param>
    public virtual void OnMurderPlayerAsTarget(MurderInfo info)
    { }

    /// <summary>
    /// 玩家死亡时调用的函数
    /// 无论死亡玩家是谁，所有玩家都会调用，所以您需要判断死亡玩家的身份
    /// </summary>
    /// <param name="player">死亡玩家</param>
    /// <param name="deathReason">死亡原因</param>
    public virtual void OnPlayerDeath(PlayerControl player, CustomDeathReason deathReason, bool isOnMeeting = false)
    { }

    /// <summary>
    /// 变形时调用的函数
    /// 不需要验证您的身份，因为调用前已经验证
    /// 请注意：全部模组端都会调用
    /// </summary>
    /// <param name="shapeshifter">变形目标</param>
    ///  /// <summary>
    /// 自視点のみ変身する
    /// 抜け殻を自視点のみに残すことが可能
    /// </summary>
    public virtual bool CanDesyncShapeshift => false;

    /// <summary>
    /// シェイプシフトチェック時に呼ばれる
    /// 自分自身が変身したときのみ呼ばれる
    /// animateを操作して変身アニメーションのカットも可能
    /// </summary>
    /// <param name="target">変身先</param>
    /// <param name="animate">アニメーションを再生するかどうか</param>
    /// <returns>falseを返すと変身がキャンセルされる</returns>
    public virtual bool OnCheckShapeshift(PlayerControl target, ref bool animate) => true;
    public virtual void OnShapeshift(PlayerControl target)
    { }

    /// <summary>
    /// 帧 Task 处理函数<br/>
    /// 不需要验证您的身份，因为调用前已经验证<br/>
    /// 请注意：全部模组端都会调用<br/>
    /// 如果您想在帧 Task 处理不是您自己时进行处理，请使用相同的参数将其实现为静态
    /// 并注册为 CustomRoleManager.OnFixedUpdateOthers
    /// </summary>
    /// <param name="player">目标玩家</param>
    public virtual void OnFixedUpdate(PlayerControl player)
    { }
    /// <summary>
    /// 秒 Task 处理函数<br/>
    /// 不需要验证您的身份，因为调用前已经验证<br/>
    /// 请注意：全部模组端都会调用<br/>
    /// </summary>
    /// <param name="player">目标玩家</param>
    /// <param name="now">当前10位时间戳</param>
    public virtual void OnSecondsUpdate(PlayerControl player, long now)
    { }

    /// <summary>
    /// 报告前检查调用的函数
    /// 与报告事件无关的玩家也会调用该函数
    /// </summary>
    /// <param name="reporter">报告者</param>
    /// <param name="target">被报告的玩家</param>
    /// <returns>false：取消报告</returns>
    public virtual bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => true;

    /// <summary>
    /// 报告时调用的函数
    /// 与报告事件无关的玩家也会调用该函数
    /// </summary>
    /// <param name="reporter">报告者</param>
    /// <param name="target">被报告的玩家</param>
    public virtual void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    { }

    /// <summary>
    /// <para>进入通风管时调用的函数</para>
    /// <para>可以取消</para>
    /// </summary>
    /// <param name="physics"></param>
    /// <param name="id">通风管 ID</param>
    /// <returns>false：将玩家被踢出通风管，其他人将看不到动画。</returns>
    public virtual bool OnEnterVent(PlayerPhysics physics, int ventId) => true;

    /// <summary>
    /// 会议开始时调用的函数
    /// </summary>
    public virtual void OnStartMeeting()
    { }

    /// <summary>
    /// 在玩家投票时触发，此时还未计票<br/>
    /// 如果返回 false，本次投票将被忽略，玩家可以再次投票<br/>
    /// 如果不想忽略投票操作本身，也不希望计票，请使用 <see cref="ModifyVote"/> 并将 doVote 设置为 false
    /// </summary>
    /// <param name="votedForId">投票给</param>
    /// <returns>如果返回 false，则假装什么都没发生，除了投票者本身谁也不知道本次投票，并且投票者可以重新投票</returns>
    public virtual bool CheckVoteAsVoter(PlayerControl votedFor) => true;

    /// <summary>
    /// 玩家投票并确定计票时调用，并可以在此处修改投票<br/>
    /// 如果希望忽略投票操作本身，请使用 <see cref="CheckVoteAsVoter"/>
    /// </summary>
    /// <param name="voterId">投票人的ID</param>
    /// <param name="sourceVotedForId">被投票人的ID</param>
    /// <returns>(修改后的被票者的ID(不修改则为 null), 修改后的票数(不修改则为 null), 是否玩家自行操作)</returns>
    public virtual (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional) => (null, null, true);

    /// <summary>
    /// 当有人投票时触发
    /// </summary>
    /// <param name="voterId">投票者的ID</param>
    /// <param name="sourceVotedForId">被投票者的ID</param>
    /// <param name="roleVoteFor">修改此值以更改投票目标</param>
    /// <param name="roleNumVotes">修改此值以更改票数</param>
    /// <param name="clearVote">改为 true 则将投票者视为未投票状态，允许其再次进行投票。但投票数据还是会计入，若多次投票将以最后一次投票的数据为准</param>
    /// <returns>false: 忽略本次投票，不计入数据</returns>
    //public virtual bool OnVote(byte voterId, byte sourceVotedForId, ref byte roleVoteFor, ref int roleNumVotes, ref bool clearVote) => true;

    /// <summary>
    /// 驱逐玩家后调用的函数
    /// </summary>
    /// <param name="exiled">被驱逐的玩家</param>
    /// <param name="DecidedWinner">是否决定了胜利玩家</param>
    public virtual void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    { }

    /// <summary>
    /// 将要驱逐玩家调用的函数
    /// </summary>
    /// <param name="exiled">被驱逐的玩家</param>
    /// <param name="DecidedWinner">是否决定了胜利玩家</param>
    /// <param name="winDescriptionText">胜利描述文本</param>
    /// <returns>OnExileWrapUp 将要执行的函数</returns>
    public virtual Action CheckExile(NetworkedPlayerInfo exiled, ref bool DecidedWinner, ref List<string> WinDescriptionText) => null;

    /// <summary>
    /// 每次会议结束后调用的函数
    /// </summary>
    public virtual void AfterMeetingTasks()
    { }

    /// <summary>
    /// 玩家发送消息后调用的函数
    /// </summary>
    /// <param name="msg">玩家发送的消息</param>
    /// <param name="recallMode">该消息应该做何处理</param>
    /// <returns>true: 阻塞该消息并停止向下判断</returns>
    public virtual bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        recallMode = MsgRecallMode.None;
        return false;
    }

    /// <summary>
    /// 每次任务完成时调用的函数
    /// 设置 cancel=true 后返回 true 来取消原版的任务完成处理
    /// </summary>
    /// <returns>true：确认覆盖</returns>
    public virtual bool OnCompleteTask(out bool cancel)
    {
        cancel = default;
        return false;
    }

    // == 破坏相关处理 ==
    /// <summary>
    /// 当玩家造成破坏时调用
    /// 若禁止将无法关门
    /// </summary>
    /// <param name="systemType">破坏的设施类型</param>
    /// <returns>false：取消破坏</returns>
    public virtual bool OnInvokeSabotage(SystemTypes systemType) => true;

    /// <summary>
    /// 当有人破坏时调用
    /// </summary>
    /// <param name="player">造成破坏的玩家</param>
    /// <param name="systemType">造成破坏的设施</param>
    /// <returns>返回false取消破坏</returns>
    public virtual bool OnSabotage(PlayerControl player, SystemTypes systemType) => true;

    // NameSystem
    // 显示的名字结构如下
    // [Role][Progress]
    // [Name][Mark]
    // [Lower][suffix]
    // Progress：任务进度、剩余子弹等信息
    // Mark：通过位置能力等进行目标标记
    // Lower：附加文本信息，模组端则会显示在屏幕下方
    // Suffix：其他信息，例如箭头

    /// <summary>
    /// 作为 seen 重写显示上的 RoleName
    /// </summary>
    /// <param name="seer">将要看到您的 RoleName 的玩家</param>
    /// <param name="enabled">是否显示 RoleName</param>
    /// <param name="roleColor">RoleName 的颜色</param>
    /// <param name="roleText">RoleName 的文本</param>
    public virtual void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText)
    { }
    /// <summary>
    /// 作为 seer 重写显示上的 RoleName
    /// </summary>
    /// <param name="seen">您将要看到其 RoleName 的玩家</param>
    /// <param name="enabled">是否显示 RoleName</param>
    /// <param name="roleColor">RoleName 的颜色</param>
    /// <param name="roleText">RoleName 的文本</param>
    public virtual void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText)
    { }
    /// <summary>
    /// 重写原来的职业名
    /// </summary>
    /// <param name="roleColor">RoleName 的颜色</param>
    /// <param name="roleText">RoleName 的文本</param>
    public virtual void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    { }
    /// <summary>
    /// 作为 seer 重写 ProgressText
    /// </summary>
    /// <param name="seen">您将要看到其 ProgressText 的玩家</param>
    /// <param name="enabled">是否显示 ProgressText</param>
    /// <param name="text">ProgressText 的文本</param>
    public virtual void OverrideProgressTextAsSeer(PlayerControl seen, ref bool enabled, ref string text)
    { }
    /// <summary>
    /// 显示在职业旁边的文本
    /// </summary>
    /// <param name="comms">目前是否为通讯破坏状态</param>
    public virtual string GetProgressText(bool comms = false) => "";
    /// <summary>
    /// 作为 seen 重写 Name
    /// </summary>
    /// <param name="seer">将要看到您的 Name 的玩家</param>
    /// <param name="nameText">Name 的文本</param>
    /// <param name="isForMeeting">是否用于显示在会议上</param>
    public virtual void OverrideNameAsSeen(PlayerControl seer, ref string nameText, bool isForMeeting = false)
    { }
    /// <summary>
    /// 作为 seer 重写 Name
    /// </summary>
    /// <param name="seen">您将要看到其 Name 的玩家</param>
    /// <param name="roleColor">Name 的颜色</param>
    /// <param name="nameText">Name 的文本</param>
    /// <param name="isForMeeting">是否用于显示在会议上</param>
    public virtual void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    { }
    /// <summary>
    /// 作为 seer 时获取 Mark 的函数
    /// 如果您想在 seer,seen 都不是您时进行处理，请使用相同的参数将其实现为静态
    /// 并注册为 CustomRoleManager.MarkOthers
    /// </summary>
    /// <param name="seer">看到的人</param>
    /// <param name="seen">被看到的人</param>
    /// <param name="isForMeeting">是否正在会议中</param>
    /// <returns>構築したMark</returns>
    public virtual string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => "";
    /// <summary>
    /// 作为 seer 时获取 LowerTex 的函数
    /// 如果您想在 seer,seen 都不是您时进行处理，请使用相同的参数将其实现为静态
    /// 并注册为 CustomRoleManager.LowerOthers
    /// </summary>
    /// <param name="seer">看到的人</param>
    /// <param name="seen">被看到的人</param>
    /// <param name="isForMeeting">是否正在会议中</param>
    /// <param name="isForHud">是否显示在模组端的HUD</param>
    /// <returns>组合后的全部 LowerText</returns>
    public virtual string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false) => "";
    /// <summary>
    /// 作为 seer 时获取 LowerTex 的函数
    /// 如果您想在 seer,seen 都不是您时进行处理，请使用相同的参数将其实现为静态
    /// 并注册为 CustomRoleManager.SuffixOthers
    /// </summary>
    /// <param name="seer">看到的人</param>
    /// <param name="seen">被看到的人</param>
    /// <param name="isForMeeting">是否正在会议中</param>
    /// <returns>组合后的全部 Suffix</returns>
    public virtual string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => "";

    /// <summary>
    /// 修改技能按钮的剩余次数
    /// </summary>
    public virtual int OverrideAbilityButtonUsesRemaining() => -1;
    /// <summary>
    /// 更改变形/跳管/生命面板按钮的文本
    /// </summary>
    public virtual bool GetAbilityButtonText(out string text)
    {
        text = default;
        return false;
    }
    /// <summary>
    /// 更改变形/跳管/生命面板按钮的图片
    /// </summary>
    /// <param name="buttonName">按钮图片名</param>
    /// <returns>true：确定要覆盖</returns>
    public virtual bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = default;
        return false;
    }
    /// <summary>
    /// 更改报告按钮的文本
    /// </summary>
    /// <param name="text">覆盖后的文本</param>
    /// <returns>true：确定要覆盖</returns>
    public virtual string GetReportButtonText() => GetString(StringNames.ReportLabel);
    /// <summary>
    /// 更改报告按钮的图片
    /// </summary>
    /// <param name="buttonName">按钮图片名</param>
    /// <returns>true：确定要覆盖</returns>
    public virtual bool GetReportButtonSprite(out string buttonName)
    {
        buttonName = default;
        return false;
    }

    /// <summary>
    /// 会议开始时的全部提示信息
    /// </summary>
    /// <param name="msgToSend">等待发送的信息列表</param>
    public virtual void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    { }

    /// <summary>
    /// 游戏开始后会立刻调用该函数
    /// 默认为全体玩家调用
    /// </summary>
    public virtual void OnGameStart()
    { }

    protected static AudioClip GetIntroSound(RoleTypes roleType) =>
        RoleManager.Instance.AllRoles.Where((role) => role.Role == roleType).FirstOrDefault().IntroSound;

    protected enum GeneralOption
    {
        Cooldown,
        KillCooldown,
        CanVent,
        VentCooldown,
        ImpostorVision,
        CanUseSabotage,
        CanCreateMadmate,
        CanKillAllies,
        CanKillSelf,
        ShapeshiftDuration,
        ShapeshiftCooldown,
    }
}
