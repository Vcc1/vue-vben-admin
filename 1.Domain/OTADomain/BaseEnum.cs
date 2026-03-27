using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTOTA.Entity.Enum
{
    /// <summary>
    /// 环境变量
    /// </summary>
    public enum Envi
    {
        /// <summary>
        /// 开发环境
        /// </summary>
        [Description("开发环境")]
        Dev,
        /// <summary>
        /// 测试环境
        /// </summary>
        [Description("测试环境")]
        Test,
        /// <summary>
        /// 生产环境
        /// </summary>
        [Description("生产环境")]
        Prod
    }

    /// <summary>
    /// 来源平台
    /// </summary>
    public enum PlatForm
    {

        ///// <summary>
        ///// 追房专用
        ///// </summary>
        //[Description("B2B")]
        //B2B =27,
        /// <summary>
        /// 携程测试供应商
        /// 携程供应商ID:16551
        /// </summary>

#if DEBUG
        [Description("携程测试供应商")]
        TEST = 16551,
        /// <summary>
        /// 湖南凯达 国内海外
        /// 携程供应商ID:8871
        /// </summary>
        [Description("湖南凯达海外")]
        HNKD = 158,
#endif
        /// <summary>
        /// 广州翼风-国内海国内
        /// 携程供应商ID:12892
        /// </summary>
        [Description("翼风国际(海外)_广州")]
        GZYF = 159,

        /// <summary>
        /// 广州玛格-国内海国内
        /// 携程供应商ID:12933
        /// </summary>
        [Description("广州玛格(国内)")]
        GZMGCN = 160,

        /// <summary>
        /// 广州玛格-国内海外
        /// 携程供应商ID:12276
        /// </summary>
        [Description("广州玛格(海外)")]
        GZMGOversea = 161,

        /// <summary>
        /// 广州杨游千里-国内海外
        /// 携程供应商ID:12982
        /// </summary>
        [Description("杨游千里(海外)_广州")]
        YYQLOversea = 163,

        /// <summary>
        /// Pull 雅格达       
        /// </summary>
        [Description("YJ")]
        Agoda = 166,

        /// <summary>
        /// 携程海外海外 广州享通
        /// </summary>
        [Description("广州享通(海外)")]
        GZXTOversea = 167,

        /// <summary>
        /// 桂林GG
        /// </summary>
        [Description("桂林NS")]
        GLNS = 168,
        /// <summary>
        /// 品凤国内
        /// </summary>
        [Description("品风国内")]
        PFCN = 169,
        /// <summary>
        /// 品风海外
        /// </summary>
        [Description("品风海外")]
        PFCNOverseas = 170,
        /// <summary>
        /// MT-Agoda
        /// </summary>
        [Description("MT-Agoda")]
        MTAgoda = 156,
        /// <summary>
        /// 热气球
        /// </summary>
        [Description("热气球")]
        NewCtripOverSeas = 115,
        /// <summary>
        /// 热气球国内
        /// </summary>
        [Description("热气球国内")]
        NewCtripCN = 171,
        /// <summary>
        /// 美团
        /// </summary>
        [Description("美团")]
        MeiTuan = 145,
        /// <summary>
        /// 道旅
        /// </summary>
        [Description("道旅")]
        DaoLv = 172,
        /// <summary>
        /// 乐天旅游
        /// </summary>
        [Description("RTX")]
        RTX = 173,
        /// <summary>
        /// 出口
        /// </summary>
        [Description("出口")]
        Export = 174,
        /// <summary>
        /// EBK
        /// </summary>
        [Description("EBK")]
        EBK = 201,
        /// <summary>
        /// 汇智
        /// </summary>
        [Description("汇智")]
        Convergent = 175,
        /// <summary>
        /// Traveloka
        /// </summary>
        [Description("Traveloka")]
        Traveloka = 176,
        /// <summary>
        /// 去哪儿
        /// </summary>
        [Description("去哪儿")]
        Qunar = 177,

        [Description("Emergingtravel")]
        ETG = 178,

        /// <summary>
        /// 
        /// </summary>
        [Description("EverRooms")]
        Everroom = 179,

        /// <summary>
        /// 萌兔飞猪海外
        /// </summary>
        [Description("萌兔飞猪海外")]
        Alitrip = 180,

        /// <summary>
        /// 萌兔飞猪海外
        /// </summary>
        [Description("同城艺龙")]
        TongCheng = 196,
        /// <summary>
        /// OTA
        /// </summary>
        [Description("OTA")]
        OTA = 999
    }



    /// <summary>
    /// 供应商订单状态
    /// </summary>
    public enum SupOrderState
    {
        /// <summary>
        /// 成功
        /// </summary>
        成功 = 1,
        /// <summary>
        /// 待确认
        /// </summary>
        待确认 = 0,
        /// <summary>
        /// 失败
        /// </summary>
        失败 = -1,
        /// <summary>
        /// 未知错误，需要客服人工跟进处理。此状态只能用于超时未响应或者供应商接口故障。 
        /// 警告：严禁在发生订单错误时，代码在不分析错误原因就返回此状态，尤其是供应商提供了如果供应商提供了容错方案，
        /// 例如当调用下单接口时发生错误时可以通过萌兔订单号查询下单是否成功时
        /// </summary>
        未知错误 = -2,
        /// <summary>
        /// 
        /// </summary>
        已取消 = 2
    }

    /// <summary>
    /// 订单流程
    /// </summary>
    public enum OrderProcess
    {

        /// <summary>
        /// 下单成功
        /// </summary>
        下单成功 = 1,
        /// <summary>
        /// 下单待确认
        /// </summary>
        下单待确认 = 2,
        /// <summary>
        /// 订单确认
        /// </summary>
        订单确认 = 3,
        /// <summary>
        /// 拒单
        /// </summary>
        拒单 = 4,
        /// <summary>
        /// 订单待取消
        /// </summary>
        订单待取消 = 5,
        /// <summary>
        /// 取消
        /// </summary>
        取消 = 6,
        /// <summary>
        /// 其它
        /// </summary>
        其它 = 7,
        /// <summary>
        /// 查询订单
        /// </summary>
        查询 = 8,
        /// <summary>
        /// 无效
        /// </summary>
        无效 = 9,
        /// <summary>
        /// 下单失败
        /// </summary>
        下单失败 = 10,
        /// <summary>
        /// 供应商自动下单
        /// </summary>
        供应商自动下单 = 101
    }


    /// <summary>
    /// OrderStatus 
    /// 说明:平台订单状态
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// 已确认
        /// </summary>
        [Description("已确认")]
        BookSuccess = 1,

        /// <summary>
        /// 待确认
        /// </summary>
        [Description("待确认")]
        Booking = 0,

        /// <summary>
        /// 已拒单
        /// </summary>
        [Description("已拒单")]
        BookFail = 10,

        /// <summary>
        /// 已取消
        /// </summary>
        [Description("已取消")]
        CancelSuccess = 201,

        /// <summary>
        /// 取消失败
        /// </summary>
        [Description("取消失败")]
        CancelFail = 202,

        /// <summary>
        /// 待取消
        /// </summary>
        [Description("待取消")]
        Canceling = 20,


        /// <summary>
        /// 无效
        /// </summary>
        [Description("无效")]
        Invalid = 30
    }





    /// <summary>
    ///价格调整  1固定值上调 2 比例值上调 3 固定值下调 4 比例值下调
    /// </summary>
    public enum AdjustPriceType
    {
        /// <summary>
        /// 固定值上调
        /// </summary>
        [Description("固定值上调")]
        固定值上调 = 1,
        /// <summary>
        /// 比例值上调
        /// </summary>
        [Description("比例值上调")]
        比例值上调 = 2,

        /// <summary>
        /// 固定值下调
        /// </summary>
        //[Description("固定值下调")]
        //固定值下调 = 3,

        /// <summary>
        /// 比例值下调
        /// </summary>
        //[Description("比例值下调")]
        //比例值下调 = 4

    }

    /// <summary>
    ///平台酒店/房型状态
    /// </summary>
    public enum PlatformStatus
    {
        /// <summary>
        /// 新建
        /// </summary>
        [Description("新建")]
        新建 = -1,

        /// <summary>
        /// 待上线
        /// </summary>
        [Description("待上线")]
        待上线 = 0,

        /// <summary>
        /// 已上线
        /// </summary>
        [Description("已上线")]
        已上线 = 1,
        /// <summary>
        /// 待下线
        /// </summary>
        [Description("待下线")]
        待下线 = 2,

        /// <summary>
        /// 已下线
        /// </summary>
        [Description("已下线")]
        已下线 = 3


    }


    /// <summary>
    /// OTA床型
    /// </summary>
    public enum BedType
    {
        /// <summary>
        /// 大床
        /// </summary>
        [Description("大床")]
        大床 = 1,
        /// <summary>
        /// 双床
        /// </summary>
        [Description("双床")]
        双床 = 2,

        /// <summary>
        /// 大床或双床
        /// </summary>
        [Description("大床或双床")]
        大床或双床 = 3,

        /// <summary>
        /// 未知
        /// </summary>
        [Description("未知")]
        未知 = 4,
        /// <summary>
        /// 单人床
        /// </summary>
        [Description("单人床")]
        单人床 = 5,
        /// <summary>
        /// 小型双人床
        /// </summary>
        [Description("小型双人床")]
        小型双人床 = 6,

    }


    /// <summary>
    /// OTA房型含窗状态
    /// </summary>
    public enum HasWindow
    {
        /// <summary>
        /// 无窗
        /// </summary>
        [Description("无窗")]
        无窗 = 0,
        /// <summary>
        /// 有窗
        /// </summary>
        [Description("有窗")]
        有窗 = 1,

        /// <summary>
        /// 部分有窗
        /// </summary>
        [Description("部分有窗")]
        部分有窗 = 2,

        /// <summary>
        /// 未知
        /// </summary>
        [Description("未知")]
        未知 = 3,

        /// <summary>
        /// 内窗
        /// </summary>
        [Description("内窗")]
        内窗 = 4,

        /// <summary>
        /// 天窗
        /// </summary>
        [Description("天窗")]
        天窗 = 5,

        /// <summary>
        /// 封闭窗
        /// </summary>
        [Description("封闭窗")]
        封闭窗 = 6,

        /// <summary>
        /// 飘窗
        /// </summary>
        [Description("飘窗")]
        飘窗 = 7
    }

    public enum DateAdjustPriceMode
    {
        /// <summary>
        /// 日期调价
        /// </summary>
        [Description("日期调价")]
        日期调价 = 0,
        /// <summary>
        /// 断房调价
        /// </summary>
        [Description("断房调价")]
        断房调价 = 1,
        /// <summary>
        /// 日期调价
        /// </summary>
        [Description("关房")]
        关房 = 2
    }

    public enum PlatFormOrderStatus
    {
        /// <summary>
        /// 已确认
        /// </summary>
        [Description("已确认")]
        BookSuccess = 1,

        /// <summary>
        /// 待确认
        /// </summary>
        [Description("待确认")]
        Booking = 0,

        /// <summary>
        /// 已拒单/确认失败
        /// </summary>
        [Description("已拒单")]
        BookFail = -1,

        #region 新增加
        /// <summary>
        /// 已取消
        /// </summary>
        [Description("已取消")]
        Canceled = -2,


        /// <summary>
        /// 拒绝取消 
        /// </summary>
        [Description("拒绝取消")]
        CancelFail = -3,


        /// <summary>
        /// 取消中
        /// </summary>
        [Description("取消中")]//还未成单，订单处于待确认状态，平台发取消通知
        Canceling = -4,


        #endregion

    }

    #region  订单跟进类型

    /// <summary>
    /// 订单跟进类型
    /// </summary>
    public enum OrderFollowType
    {
        /// <summary>
        /// 未定义
        /// </summary>
        [Description("未定义")]
        UnKnow = 0,
        /// <summary>
        /// 其他
        /// </summary>
        [Description("其他")]
        Others = 1,

        /// <summary>
        /// 房型不符
        /// </summary>
        [Description("房型不符")]
        RoomNoMatch = 101,

        /// <summary>
        /// 查无预定
        /// </summary>
        [Description("查无预定")]
        CheckNoReservation = 102,
        /// <summary>
        /// 高价单
        /// </summary>
        [Description("高价单")]
        HihgPriceOrder = 103,
        /// <summary>
        /// 变更
        /// </summary>
        [Description("变更")]
        Reschedule = 201,

        /// <summary>
        /// 免费取消
        /// </summary>
        [Description("免费取消")]
        FreeCancle = 211,

        /// <summary>
        /// 增加备注
        /// </summary>
        [Description("增加备注")]
        AddRemark = 221,

        /// <summary>
        /// 申请确认号
        /// </summary>
        [Description("申请确认号")]
        ApplyConfirmNo = 222,

        /// <summary>
        /// 提供备选
        /// </summary>
        [Description("提供备选")]
        Alternative = 301,

        /// <summary>
        /// 拆单
        /// </summary>
        [Description("拆单")]
        SpiltOrder = 401,

        /// <summary>
        /// 测试单
        /// </summary>
        [Description("测试单")]
        TestOrder = 402,

        /// <summary>
        /// ARPrevention
        /// </summary>
        [Description("ARPrevention")]
        ARPrevention = 501,

        /// <summary>
        /// 取消跟进
        /// </summary>
        [Description("取消跟进")]
        CancleProcessStatus = 9001,

        /// <summary>
        /// 床型不符
        /// </summary>
        //[Description("床型不符")]
        //BedNoMatch = 101,
        /// <summary>
        /// 餐食不符
        /// </summary>
        //[Description("餐食不符")]
        //MealNoMatch = 103,
        /// <summary>
        /// 改期
        /// </summary>
        //[Description("改期")]
        //Reschedule = 201,

        /// <summary>
        /// 改入住人
        /// </summary>
        //[Description("改入住人")]
        //ChangeName = 202,

        /// <summary>
        /// 改床型
        /// </summary>
        //[Description("改床型")]
        //ChangeBed = 203,

        /// <summary>
        /// 改房型
        /// </summary>
        //[Description("改房型")]
        //ChangeRoom = 204,

        /// <summary>
        /// 带罚金取消
        /// </summary>
        //[Description("带罚金取消")]
        //CancelWithPenalty = 212,
        /// <summary>
        /// 原单保留
        /// </summary>
        //[Description("原单保留")]
        //BookSuccess = 213,
        /// <summary>
        /// 升级房型
        /// </summary>
        //[Description("升级房型")]
        //UpgradeRoom = 302,
        /// <summary>
        /// 降级房型
        /// </summary>
        //[Description("降级房型")]
        //DowngradeRoom = 303,
        /// <summary>
        /// 退早
        /// </summary>
        //[Description("退早加早")]
        //RefundMeal = 304,
        /// <summary>
        /// 退费
        /// </summary>
        //[Description("退费")]
        //RefundPrice = 305,
        /// <summary>
        /// 推送确认号
        /// </summary>
        //[Description("推送确认号")]
        //PushConfimNo = 306,









    }
    #endregion

    public enum Gender
    {
        [Description("男")]
        Male = 2,

        /// <summary>
        /// 待确认
        /// </summary>
        [Description("女")]
        FeMale = 1
    }

    public enum AppendRoomStatus
    {
        /// <summary>
        /// 追房失败
        /// </summary>
        [Description("追房失败")]
        追房失败 = -1,
        /// <summary>
        /// 未追房
        /// </summary>
        [Description("未追房")]
        未追房 = 0,
        /// <summary>
        /// 未追房
        /// </summary>
        [Description("追房中")]
        追房中 = 1,
        /// <summary>
        /// 未追房
        /// </summary>
        [Description("追房成功")]
        追房成功 = 2
    }
}
