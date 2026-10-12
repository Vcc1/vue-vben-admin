using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sup.InterFace.SupBaseApi.EANEPS.Entitys.EanHotelInfo
{
    /// <summary>
    /// EAN 酒店静态信息响应实体。
    /// </summary>
    public class EPSHotelInfoRespone
    {
        /// <summary>
        /// 酒店唯一标识。
        /// </summary>
        [JsonPropertyName("property_id")]
        public string PropertyId { get; set; }

        /// <summary>
        /// 酒店名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 酒店地址信息。
        /// </summary>
        [JsonPropertyName("address")]
        public Address Address { get; set; }

        /// <summary>
        /// 酒店评分信息。
        /// </summary>
        [JsonPropertyName("ratings")]
        public Ratings Ratings { get; set; }

        /// <summary>
        /// 酒店位置信息。
        /// </summary>
        [JsonPropertyName("location")]
        public Location Location { get; set; }

        /// <summary>
        /// 酒店联系电话。
        /// </summary>
        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        /// <summary>
        /// 酒店传真号码。
        /// </summary>
        [JsonPropertyName("fax")]
        public string Fax { get; set; }

        /// <summary>
        /// 酒店分类信息。
        /// </summary>
        [JsonPropertyName("category")]
        public Category Category { get; set; }

        /// <summary>
        /// 酒店排序或排名。
        /// </summary>
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// 酒店业务模式信息。
        /// </summary>
        [JsonPropertyName("business_model")]
        public Business_Model BusinessModel { get; set; }

        /// <summary>
        /// 入住规则信息。
        /// </summary>
        [JsonPropertyName("checkin")]
        public Checkin Checkin { get; set; }

        /// <summary>
        /// 退房规则信息。
        /// </summary>
        [JsonPropertyName("checkout")]
        public Checkout Checkout { get; set; }

        /// <summary>
        /// 费用说明信息。
        /// </summary>
        [JsonPropertyName("fees")]
        public Fees Fees { get; set; }

        /// <summary>
        /// 酒店政策信息。
        /// </summary>
        [JsonPropertyName("policies")]
        public Policies Policies { get; set; }

        /// <summary>
        /// 酒店属性集合。
        /// </summary>
        [JsonPropertyName("attributes")]
        public Attributes Attributes { get; set; }

        /// <summary>
        /// 酒店设施集合。
        /// </summary>
        [JsonPropertyName("amenities")]
        public Dictionary<string, Amenitie> Amenities { get; set; }

        /// <summary>
        /// 酒店图片集合。
        /// </summary>
        [JsonPropertyName("images")]
        public Image[] Images { get; set; }

        /// <summary>
        /// 到店支付信息。
        /// </summary>
        [JsonPropertyName("onsite_payments")]
        public Onsite_Payments OnsitePayments { get; set; }

        //public Rooms rooms { get; set; }
        /// <summary>
        /// 房型信息集合。
        /// </summary>
        [JsonPropertyName("rooms")]
        public Dictionary<string, Room> Rooms { get; set; }

        /// <summary>
        /// 价格信息集合。
        /// </summary>
        [JsonPropertyName("rates")]
        public Dictionary<string, Rate> Rates { get; set; }

        /// <summary>
        /// 数据时间信息。
        /// </summary>
        [JsonPropertyName("dates")]
        public Dates Dates { get; set; }

        /// <summary>
        /// 酒店描述信息。
        /// </summary>
        [JsonPropertyName("descriptions")]
        public Descriptions1 Descriptions { get; set; }

        //public Statistics statistics { get; set; }
        /// <summary>
        /// 统计信息集合。
        /// </summary>
        [JsonPropertyName("statistics")]
        public Dictionary<string, Statistic> Statistics { get; set; }

        /// <summary>
        /// 机场信息。
        /// </summary>
        [JsonPropertyName("airports")]
        public Airports Airports { get; set; }

        /// <summary>
        /// 注册编号。
        /// </summary>
        [JsonPropertyName("registry_number")]
        public string RegistryNumber { get; set; }

        //public Themes themes { get; set; }
        /// <summary>
        /// 主题信息集合。
        /// </summary>
        [JsonPropertyName("themes")]
        public Dictionary<string, Theme> Themes { get; set; }

        /// <summary>
        /// 全包服务信息。
        /// </summary>
        [JsonPropertyName("all_inclusive")]
        public All_Inclusive AllInclusive { get; set; }

        /// <summary>
        /// 税务编号。
        /// </summary>
        [JsonPropertyName("tax_id")]
        public string TaxId { get; set; }

        /// <summary>
        /// 连锁信息。
        /// </summary>
        [JsonPropertyName("chain")]
        public Chain Chain { get; set; }

        /// <summary>
        /// 品牌信息。
        /// </summary>
        [JsonPropertyName("brand")]
        public Brand Brand { get; set; }

        /// <summary>
        /// 酒店支持语言集合。
        /// </summary>
        [JsonPropertyName("spoken_languages")]
        public Dictionary<string, SpokenLanguages> SpokenLanguages { get; set; }

        /// <summary>
        /// 是否为多单元酒店。
        /// </summary>
        [JsonPropertyName("multi_unit")]
        public bool MultiUnit { get; set; }

        /// <summary>
        /// 是否建议支付登记。
        /// </summary>
        [JsonPropertyName("payment_registration_recommended")]
        public bool PaymentRegistrationRecommended { get; set; }

        /// <summary>
        /// 供应来源。
        /// </summary>
        [JsonPropertyName("supply_source")]
        public string SupplySource { get; set; }
    }

    /// <summary>
    /// 酒店支持语言实体。
    /// </summary>
    public class SpokenLanguages
    {
        /// <summary>
        /// 语言标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 语言名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
    /// <summary>
    /// 连锁信息实体。
    /// </summary>
    public class Chain
    {
        /// <summary>
        /// 连锁标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 连锁名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 连锁下品牌集合。
        /// </summary>
        [JsonPropertyName("brands")]
        public Dictionary<string, Brand> Brands { get; set; }
    }
    /// <summary>
    /// 品牌信息实体。
    /// </summary>
    public class Brand
    {
        /// <summary>
        /// 品牌标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 品牌名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
    /// <summary>
    /// 酒店地址信息实体。
    /// </summary>
    public class Address
    {
        /// <summary>
        /// 地址第一行。
        /// </summary>
        [JsonPropertyName("line_1")]
        public string Line1 { get; set; }

        /// <summary>
        /// 地址第二行。
        /// </summary>
        [JsonPropertyName("line_2")]
        public string Line2 { get; set; }

        /// <summary>
        /// 城市名称。
        /// </summary>
        [JsonPropertyName("city")]
        public string City { get; set; }

        /// <summary>
        /// 州省代码。
        /// </summary>
        [JsonPropertyName("state_province_code")]
        public string StateProvinceCode { get; set; }

        /// <summary>
        /// 州省名称。
        /// </summary>
        [JsonPropertyName("state_province_name")]
        public string StateProvinceName { get; set; }

        /// <summary>
        /// 邮政编码。
        /// </summary>
        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; }

        /// <summary>
        /// 国家代码。
        /// </summary>
        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; }

        /// <summary>
        /// 是否需要地址脱敏。
        /// </summary>
        [JsonPropertyName("obfuscation_required")]
        public bool ObfuscationRequired { get; set; }

        /// <summary>
        /// 本地化地址内容。
        /// </summary>
        [JsonPropertyName("localized")]
        public Localized Localized { get; set; }
    }
    /// <summary>
    /// 本地化地址信息实体。
    /// </summary>
    public class Localized
    {
        /// <summary>
        /// 本地化链接集合。
        /// </summary>
        [JsonPropertyName("links")]
        public Dictionary<string, Link> Links { get; set; }
    }
    /// <summary>
    /// 酒店评分信息实体。
    /// </summary>
    public class Ratings
    {
        /// <summary>
        /// 官方评分信息。
        /// </summary>
        [JsonPropertyName("property")]
        public Property Property { get; set; }

        /// <summary>
        /// 住客评分信息。
        /// </summary>
        [JsonPropertyName("guest")]
        public GuestRating Guest { get; set; }
    }

    /// <summary>
    /// 住客评分信息实体。
    /// </summary>
    public class GuestRating
    {
        /// <summary>
        /// 评分数量。
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// 综合评分。
        /// </summary>
        [JsonPropertyName("overall")]
        public string Overall { get; set; }

        /// <summary>
        /// 清洁度评分。
        /// </summary>
        [JsonPropertyName("cleanliness")]
        public string Cleanliness { get; set; }

        /// <summary>
        /// 服务评分。
        /// </summary>
        [JsonPropertyName("service")]
        public string Service { get; set; }

        /// <summary>
        /// 舒适度评分。
        /// </summary>
        [JsonPropertyName("comfort")]
        public string Comfort { get; set; }

        /// <summary>
        /// 设施状况评分。
        /// </summary>
        [JsonPropertyName("condition")]
        public string Condition { get; set; }

        /// <summary>
        /// 位置评分。
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; }

        /// <summary>
        /// 周边环境评分。
        /// </summary>
        [JsonPropertyName("neighborhood")]
        public string Neighborhood { get; set; }

        /// <summary>
        /// 品质评分。
        /// </summary>
        [JsonPropertyName("quality")]
        public string Quality { get; set; }

        /// <summary>
        /// 性价比评分。
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// 设施评分。
        /// </summary>
        [JsonPropertyName("amenities")]
        public string Amenities { get; set; }

        /// <summary>
        /// 推荐百分比。
        /// </summary>
        [JsonPropertyName("recommendation_percent")]
        public string RecommendationPercent { get; set; }
    }
    /// <summary>
    /// 酒店星级评分实体。
    /// </summary>
    public class Property
    {
        /// <summary>
        /// 评分值。
        /// </summary>
        [JsonPropertyName("rating")]
        public string Rating { get; set; }

        /// <summary>
        /// 评分类型。
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// 酒店位置信息实体。
    /// </summary>
    public class Location
    {
        /// <summary>
        /// 经纬度坐标信息。
        /// </summary>
        [JsonPropertyName("coordinates")]
        public Coordinates Coordinates { get; set; }
    }

    /// <summary>
    /// 经纬度信息实体。
    /// </summary>
    public class Coordinates
    {
        /// <summary>
        /// 纬度。
        /// </summary>
        [JsonPropertyName("latitude")]
        public decimal Latitude { get; set; }

        /// <summary>
        /// 经度。
        /// </summary>
        [JsonPropertyName("longitude")]
        public decimal Longitude { get; set; }
    }

    /// <summary>
    /// 酒店分类信息实体。
    /// </summary>
    public class Category
    {
        /// <summary>
        /// 分类标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 分类名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// 业务模式信息实体。
    /// </summary>
    public class Business_Model
    {
        /// <summary>
        /// 是否支持 Expedia 收款。
        /// </summary>
        [JsonPropertyName("expedia_collect")]
        public bool ExpediaCollect { get; set; }

        /// <summary>
        /// 是否支持酒店收款。
        /// </summary>
        [JsonPropertyName("property_collect")]
        public bool PropertyCollect { get; set; }
    }

    /// <summary>
    /// 入住信息实体。
    /// </summary>
    public class Checkin
    {
        /// <summary>
        /// 是否支持 24 小时入住。
        /// </summary>
        [JsonPropertyName("24_hour")]
        public string TwentyFourHour { get; set; }

        /// <summary>
        /// 最早入住时间。
        /// </summary>
        [JsonPropertyName("begin_time")]
        public string BeginTime { get; set; }

        /// <summary>
        /// 最晚入住时间。
        /// </summary>
        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        /// <summary>
        /// 入住说明。
        /// </summary>
        [JsonPropertyName("instructions")]
        public string Instructions { get; set; }

        /// <summary>
        /// 特殊入住说明。
        /// </summary>
        [JsonPropertyName("special_instructions")]
        public string SpecialInstructions { get; set; }

        /// <summary>
        /// 最小入住年龄。
        /// </summary>
        [JsonPropertyName("min_age")]
        public int MinAge { get; set; }
    }

    /// <summary>
    /// 退房信息实体。
    /// </summary>
    public class Checkout
    {
        /// <summary>
        /// 退房时间。
        /// </summary>
        [JsonPropertyName("time")]
        public string Time { get; set; }
    }

    /// <summary>
    /// 费用说明实体。
    /// </summary>
    public class Fees
    {
        /// <summary>
        /// 必付费用说明。
        /// </summary>
        [JsonPropertyName("mandatory")]
        public string Mandatory { get; set; }

        /// <summary>
        /// 可选费用说明。
        /// </summary>
        [JsonPropertyName("optional")]
        public string Optional { get; set; }
    }

    /// <summary>
    /// 酒店政策信息实体。
    /// </summary>
    public class Policies
    {
        /// <summary>
        /// 入住前须知。
        /// </summary>
        [JsonPropertyName("know_before_you_go")]
        public string KnowBeforeYouGo { get; set; }
    }

    /// <summary>
    /// 酒店属性集合实体。
    /// </summary>
    public class Attributes
    {
        //public General general { get; set; }

        /// <summary>
        /// 通用属性集合。
        /// </summary>
        [JsonPropertyName("generals")]
        public Dictionary<string, Attribute> Generals { get; set; }

        /// <summary>
        /// 宠物政策属性集合。
        /// </summary>
        [JsonPropertyName("pets")]
        public Dictionary<string, Attribute> Pets { get; set; }

        //public Pets pets { get; set; }
    }

    /// <summary>
    /// 酒店属性项实体。
    /// </summary>
    public class Attribute
    {
        /// <summary>
        /// 属性标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 属性名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 属性值。
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }


    /// <summary>
    /// 房价维度信息实体。
    /// </summary>
    public class Rate
    {
        /// <summary>
        /// 价格标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 价格包含的设施集合。
        /// </summary>
        [JsonPropertyName("amenities")]
        public Dictionary<string, Amenitie> Amenities { get; set; }

        /// <summary>
        /// 特惠说明。
        /// </summary>
        [JsonPropertyName("special_offer_description")]
        public string SpecialOfferDescription { get; set; }
    }


    /// <summary>
    /// 到店支付信息实体。
    /// </summary>
    public class Onsite_Payments
    {
        /// <summary>
        /// 到店支付币种。
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        /// <summary>
        /// 到店支付类型集合。
        /// </summary>
        [JsonPropertyName("types")]
        public Dictionary<string, Types> Types { get; set; }
    }
    /// <summary>
    /// 到店支付类型实体。
    /// </summary>
    public class Types
    {
        /// <summary>
        /// 类型标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 类型名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }


    /// <summary>
    /// 房型静态信息实体。
    /// </summary>
    public class Room
    {
        /// <summary>
        /// 房型标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 房型名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 房型描述信息。
        /// </summary>
        [JsonPropertyName("descriptions")]
        public Descriptions Descriptions { get; set; }

        /// <summary>
        /// 房型设施集合。
        /// </summary>
        [JsonPropertyName("amenities")]
        public Dictionary<string, Amenitie> Amenities { get; set; }

        /// <summary>
        /// 房型图片集合。
        /// </summary>
        [JsonPropertyName("images")]
        public List<Image> Images { get; set; }

        /// <summary>
        /// 床型组合集合。
        /// </summary>
        [JsonPropertyName("bed_groups")]
        public Dictionary<string, BedGroup> BedGroups { get; set; }

        /// <summary>
        /// 房间面积信息。
        /// </summary>
        [JsonPropertyName("area")]
        public Area Area { get; set; }

        /// <summary>
        /// 可入住人数信息。
        /// </summary>
        [JsonPropertyName("occupancy")]
        public Occupancy Occupancy { get; set; }

        /// <summary>
        /// 房型景观集合。
        /// </summary>
        [JsonPropertyName("views")]
        public Dictionary<string, View> Views { get; set; }
    }

    /// <summary>
    /// 面积
    /// </summary>
    public class Area
    {
        /// <summary>
        /// 平方米面积。
        /// </summary>
        [JsonPropertyName("square_meters")]
        public decimal? SquareMeters { get; set; }

        /// <summary>
        /// 平方英尺面积。
        /// </summary>
        [JsonPropertyName("square_feet")]
        public decimal? SquareFeet { get; set; }
    }



    #region 最大入住人数
    /// <summary>
    /// 入住人数信息实体。
    /// </summary>
    public class Occupancy
    {
        /// <summary>
        /// 最大允许入住人数。
        /// </summary>
        [JsonPropertyName("max_allowed")]
        public Max_Allowed MaxAllowed { get; set; }

        /// <summary>
        /// 年龄分类信息。
        /// </summary>
        [JsonPropertyName("age_categories")]
        public Age_Categories AgeCategories { get; set; }
    }

    /// <summary>
    /// 最大允许入住人数实体。
    /// </summary>
    public class Max_Allowed
    {
        /// <summary>
        /// 最大总人数。
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// 最大儿童人数。
        /// </summary>
        [JsonPropertyName("children")]
        public int Children { get; set; }

        /// <summary>
        /// 最大成人人数。
        /// </summary>
        [JsonPropertyName("adults")]
        public int Adults { get; set; }
    }

    /// <summary>
    /// 年龄分类集合实体。
    /// </summary>
    public class Age_Categories
    {
        /// <summary>
        /// 年龄分类明细集合。
        /// </summary>
        [JsonPropertyName("age_Categories")]
        public Dictionary<string, Age_Categorie> AgeCategories { get; set; }
    }

    public class Age_Categorie
    {
        /// <summary>
        /// 年龄分类名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        /// <summary>
        /// 最小年龄
        /// </summary>
        [JsonPropertyName("minimum_age")]
        public int MinimumAge { get; set; }
    }

    #endregion


    #region 景观

    /// <summary>
    /// 房间景观实体。
    /// </summary>
    public class View
    {
        /// <summary>
        /// 景观标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 景观名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
    #endregion

    /// <summary>
    /// 房型描述实体。
    /// </summary>
    public class Descriptions
    {
        /// <summary>
        /// 房型概述。
        /// </summary>
        [JsonPropertyName("overview")]
        public string Overview { get; set; }
    }


    /// <summary>
    /// 设施信息实体。
    /// </summary>
    public class Amenitie
    {
        /// <summary>
        /// 设施标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 设施名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 设施值。
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// 所属分类集合。
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }
    }

    /// <summary>
    /// 床型组合实体。
    /// </summary>
    public class BedGroup
    {
        /// <summary>
        /// 床型组合标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 床型组合描述。
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// 床型配置集合。
        /// </summary>
        [JsonPropertyName("configuration")]
        public Configuration[] Configuration { get; set; }
    }

    /// <summary>
    /// 床型配置实体。
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// 床型类型。
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// 床尺寸。
        /// </summary>
        [JsonPropertyName("size")]
        public string Size { get; set; }

        /// <summary>
        /// 数量。
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 图片信息实体。
    /// </summary>
    public class Image
    {
        /// <summary>
        /// 图片标题。
        /// </summary>
        [JsonPropertyName("caption")]
        public string Caption { get; set; }

        /// <summary>
        /// 是否为主图。
        /// </summary>
        [JsonPropertyName("hero_image")]
        public bool HeroImage { get; set; }

        /// <summary>
        /// 图片分类。
        /// </summary>
        [JsonPropertyName("category")]
        public long Category { get; set; }

        /// <summary>
        /// 图片链接集合。
        /// </summary>
        [JsonPropertyName("links")]
        public Dictionary<string, Link> Links { get; set; }
    }


    /// <summary>
    /// 链接信息实体。
    /// </summary>
    public class Link
    {
        /// <summary>
        /// 请求方法。
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; }

        /// <summary>
        /// 链接地址。
        /// </summary>
        [JsonPropertyName("href")]
        public string Href { get; set; }

        /// <summary>
        /// 过期时间。
        /// </summary>
        [JsonPropertyName("expires")]
        public string Expires { get; set; }
    }


    //public class Rates
    //{
    //    public _333Abc _333abc { get; set; }
    //}

    //public class _333Abc
    //{
    //    public string id { get; set; }
    //    public Amenities2 amenities { get; set; }
    //}

    //public class Amenities2
    //{
    //    public _12341 _1234 { get; set; }
    //    public _2104 _2104 { get; set; }
    //}

    //public class _12341
    //{
    //    public int id { get; set; }
    //    public string name { get; set; }
    //    public string value { get; set; }
    //}

    //public class _2104
    //{
    //    public int id { get; set; }
    //    public string name { get; set; }
    //}

    /// <summary>
    /// 数据时间信息实体。
    /// </summary>
    public class Dates
    {
        /// <summary>
        /// 新增时间。
        /// </summary>
        [JsonPropertyName("added")]
        public string Added { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        [JsonPropertyName("updated")]
        public string Updated { get; set; }
    }

    /// <summary>
    /// 酒店描述信息实体。
    /// </summary>
    public class Descriptions1
    {
        /// <summary>
        /// 设施描述。
        /// </summary>
        [JsonPropertyName("amenities")]
        public string Amenities { get; set; }

        /// <summary>
        /// 餐饮描述。
        /// </summary>
        [JsonPropertyName("dining")]
        public string Dining { get; set; }

        /// <summary>
        /// 装修描述。
        /// </summary>
        [JsonPropertyName("renovations")]
        public string Renovations { get; set; }

        /// <summary>
        /// 国家评级说明。
        /// </summary>
        [JsonPropertyName("national_ratings")]
        public string NationalRatings { get; set; }

        /// <summary>
        /// 商务设施描述。
        /// </summary>
        [JsonPropertyName("business_amenities")]
        public string BusinessAmenities { get; set; }

        /// <summary>
        /// 客房描述。
        /// </summary>
        [JsonPropertyName("rooms")]
        public string Rooms { get; set; }

        /// <summary>
        /// 周边景点描述。
        /// </summary>
        [JsonPropertyName("attractions")]
        public string Attractions { get; set; }

        /// <summary>
        /// 位置描述。
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; }

        /// <summary>
        /// 标题描述。
        /// </summary>
        [JsonPropertyName("headline")]
        public string Headline { get; set; }

        /// <summary>
        /// 通用描述。
        /// </summary>
        [JsonPropertyName("general")]
        public string General { get; set; }
    }


    /// <summary>
    /// 统计信息实体。
    /// </summary>
    public class Statistic
    {
        /// <summary>
        /// 统计项标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 统计项名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 统计项值。
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    //public class _54
    //{
    //    public int id { get; set; }
    //    public string name { get; set; }
    //    public string value { get; set; }
    //}

    /// <summary>
    /// 机场信息实体。
    /// </summary>
    public class Airports
    {
        /// <summary>
        /// 首选机场信息。
        /// </summary>
        [JsonPropertyName("preferred")]
        public Preferred Preferred { get; set; }
    }

    /// <summary>
    /// 首选机场信息实体。
    /// </summary>
    public class Preferred
    {
        /// <summary>
        /// IATA 机场代码。
        /// </summary>
        [JsonPropertyName("iata_airport_code")]
        public string IataAirportCode { get; set; }
    }

    //public class Themes
    //{
    //    public _2337 _2337 { get; set; }
    //    public _2341 _2341 { get; set; }
    //}

    /// <summary>
    /// 酒店主题信息实体。
    /// </summary>
    public class Theme
    {
        /// <summary>
        /// 主题标识。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 主题名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 主题值。
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    /// <summary>
    /// 全包服务信息实体。
    /// </summary>
    public class All_Inclusive
    {
        /// <summary>
        /// 是否全部价型为全包。
        /// </summary>
        [JsonPropertyName("all_rate_plans")]
        public bool AllRatePlans { get; set; }

        /// <summary>
        /// 是否部分价型为全包。
        /// </summary>
        [JsonPropertyName("some_rate_plans")]
        public bool SomeRatePlans { get; set; }

        /// <summary>
        /// 全包服务详情。
        /// </summary>
        [JsonPropertyName("details")]
        public string Details { get; set; }
    }
}
