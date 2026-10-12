# MengTu.Connectmate.Client 使用和配置说明

## 1. 类库定位

`MengTu.Connectmate.Client` 用于从 Connectmate 服务端拉取数据库连接配置，并把结果动态注入到当前应用的 `IConfiguration`。

适用场景：
- ASP.NET Core
- Worker Service
- Console / 定时任务
- 需要统一管理数据库连接配置的服务

支持目标框架：
- `.NET 8`
- `.NET 10`

---

## 2. 接入方式

### 2.1 注册服务

最常用方式：直接读取默认配置节 `Connectmate:Client`

```csharp
using MengTu.Connectmate.Client;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddConnectmateRemoteConfiguration(builder.Configuration);
```

对应默认读取：

```json
{
  "Connectmate": {
    "Client": {
    }
  }
}
```

### 2.2 指定自定义配置节

如果不想用默认的 `Connectmate:Client`，可以指定配置节路径：

```csharp
builder.Services.AddConnectmateRemoteConfiguration(
    builder.Configuration,
    "Infrastructure:Connectmate");
```

### 2.3 代码里直接配置

也可以完全不依赖固定配置节，直接在代码中绑定：

```csharp
builder.Services.AddConnectmateRemoteConfiguration(
    builder.Configuration,
    options =>
    {
        builder.Configuration.GetSection("Infrastructure:Connectmate").Bind(options);
    });
```

---

## 3. 标准配置示例

```json
{
  "Connectmate": {
    "Client": {
      "BaseAddress": "http://localhost:5296",
      "ApiKeyId": "your-api-key-id",
      "ClientPrivateKey": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
      "ServerPublicKey": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
      "PollIntervalSeconds": 60,
      "UserAgent": "YourAppName",
      "IsInternalNetwork": false,
      "Subscriptions": [
        {
          "InstanceCode": "roommappingredis",
          "EnvCode": "dev"
        }
      ]
    }
  }
}
```

字段说明：

- `BaseAddress`：Connectmate 服务地址
- `ApiKeyId`：客户端使用的 ApiKey 标识
- `ClientPrivateKey`：客户端私钥，用于签名请求
- `ServerPublicKey`：服务端公钥，用于校验响应/解密流程
- `PollIntervalSeconds`：轮询间隔，单位秒
- `UserAgent`：客户端标识，会随请求一起上报
- `IsInternalNetwork`：当前客户端拉取内网还是外网配置
- `Subscriptions`：要订阅的数据库配置列表

---

## 4. Subscriptions 怎么配置

`Subscriptions` 支持配置多个订阅项，每个订阅项表示一组：
- 数据库实例 `InstanceCode`
- 环境 `EnvCode`
- 注入目标节点 `ConfigurationSection`（可选）
- 客户端格式模板 `FormatString`（可选）

示例：

```json
{
  "Subscriptions": [
    {
      "InstanceCode": "roommappingredis",
      "EnvCode": "dev"
    },
    {
      "InstanceCode": "hotelmaster",
      "EnvCode": "prod",
      "ConfigurationSection": "MyApp:Databases:HotelMaster"
    }
  ]
}
```

---

## 5. 配置会被注入到哪里

### 5.1 不指定 `ConfigurationSection`

如果订阅项没有配置 `ConfigurationSection`，默认注入到：

```plaintext
ConnectionStrings:{InstanceCode}
```

例如：

```json
{
  "Subscriptions": [
    {
      "InstanceCode": "roommappingredis",
      "EnvCode": "dev"
    }
  ]
}
```

会生成：

```plaintext
ConnectionStrings:roommappingredis
```

业务代码读取方式：

```csharp
var connectionString = builder.Configuration.GetConnectionString("roommappingredis");
```

### 5.2 指定 `ConfigurationSection`

如果指定了 `ConfigurationSection`，则会把配置注入到你指定的节点下。

例如：

```json
{
  "Subscriptions": [
    {
      "InstanceCode": "hotelmaster",
      "EnvCode": "prod",
      "ConfigurationSection": "MyApp:Databases:HotelMaster"
    }
  ]
}
```

会生成一组子节点：

```plaintext
MyApp:Databases:HotelMaster:InstanceCode
MyApp:Databases:HotelMaster:Environment
MyApp:Databases:HotelMaster:IsInternalNetwork
MyApp:Databases:HotelMaster:Version
MyApp:Databases:HotelMaster:Type
MyApp:Databases:HotelMaster:ConfigHash
MyApp:Databases:HotelMaster:RawJson
MyApp:Databases:HotelMaster:Serialized
MyApp:Databases:HotelMaster:ConnectionString
MyApp:Databases:HotelMaster:Fields:host
MyApp:Databases:HotelMaster:Fields:port
```

业务代码读取方式：

```csharp
var section = builder.Configuration.GetSection("MyApp:Databases:HotelMaster");
var connectionString = section["ConnectionString"];
var host = section["Fields:host"];
var port = section["Fields:port"];
```

如果只是想像传统方式那样拿连接串，建议直接读取：

```csharp
var connectionString = builder.Configuration["MyApp:Databases:HotelMaster:ConnectionString"];
```

---

## 6. 推荐配置方式

### 场景一：只关心连接串

推荐不写 `ConfigurationSection`，直接走默认 `ConnectionStrings:{InstanceCode}`。

```json
{
  "Subscriptions": [
    {
      "InstanceCode": "roommappingredis",
      "EnvCode": "dev"
    }
  ]
}
```

优点：
- 现有代码改动最小
- 可以直接使用 `GetConnectionString(...)`

### 场景二：既要连接串，也要字段明细/版本信息

推荐配置 `ConfigurationSection`。

```json
{
  "Subscriptions": [
    {
      "InstanceCode": "hotelmaster",
      "EnvCode": "prod",
      "ConfigurationSection": "MyApp:Databases:HotelMaster"
    }
  ]
}
```

优点：
- 可以拿到连接串
- 也可以拿到原始字段、版本、Hash、原始 JSON
- 更适合做诊断、调试和扩展

---

## 7. 运行时行为说明

客户端启动后行为如下：

1. 启动阶段先尝试首次远程拉取
2. 首次拉取成功后，后台按 `PollIntervalSeconds` 周期轮询
3. 如果远程失败，会尝试回退到本地缓存
4. 如果连接串从“有值”变成“空值”，新配置会被拒绝应用并记录错误日志
5. 首次加载成功、后续配置变化、远程拉取失败，都会打印日志

这意味着：
- 应用不会在首次远程配置完全不可用且本地也无缓存时静默成功
- 有缓存时可以在远程不可用场景下继续运行

---

## 8. 与同事约定的配置建议

建议统一遵循以下约定：

- `UserAgent` 使用应用名，例如：`Hotel.SyncService`
- 私钥不要直接写死在仓库里，优先放：
  - 环境变量
  - 用户机密
  - 配置中心
- `InstanceCode`、`EnvCode` 使用平台统一编码，不要自造别名
- 只要业务代码依赖 `GetConnectionString(...)`，就不要配置 `ConfigurationSection`
- 只要需要字段明细、版本、原始 JSON，再使用 `ConfigurationSection`

---

## 9. 最小可运行示例

```csharp
using MengTu.Connectmate.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddConnectmateRemoteConfiguration(builder.Configuration);

await builder.Build().RunAsync();
```

对应配置：

```json
{
  "Connectmate": {
    "Client": {
      "BaseAddress": "http://localhost:5296",
      "ApiKeyId": "your-api-key-id",
      "ClientPrivateKey": "your-private-key",
      "ServerPublicKey": "your-server-public-key",
      "PollIntervalSeconds": 60,
      "UserAgent": "Connectmate.ClientConfigTestApp",
      "IsInternalNetwork": false,
      "Subscriptions": [
        {
          "InstanceCode": "roommappingredis",
          "EnvCode": "dev"
        }
      ]
    }
  }
}
```

---

## 10. 常见问题

### Q1：`Subscriptions` 可以注入到自定义 config 节点吗？

可以。

通过给订阅项增加 `ConfigurationSection` 即可，例如：

```json
{
  "InstanceCode": "hotelmaster",
  "EnvCode": "prod",
  "ConfigurationSection": "MyApp:Databases:HotelMaster"
}
```

### Q2：不配 `ConfigurationSection` 时写到哪里？

默认写到：

```plaintext
ConnectionStrings:{InstanceCode}
```

### Q3：一个应用能订阅多个数据库吗？

可以，`Subscriptions` 本身就是数组。

### Q4：能不能读取原始字段而不只读取连接串？

可以。配置 `ConfigurationSection` 后，可以从：

```plaintext
{Section}:Fields:{FieldCode}
```

读取原始字段值。

---

## 11. 示例：给同事的推荐模板

```json
{
  "Connectmate": {
    "Client": {
      "BaseAddress": "http://connectmate-api",
      "ApiKeyId": "replace-with-real-apikey",
      "ClientPrivateKey": "replace-with-private-key",
      "ServerPublicKey": "replace-with-server-public-key",
      "ServerKeyId": "default",
      "PollIntervalSeconds": 60,
      "UserAgent": "YourServiceName",
      "IsInternalNetwork": false,
      "Subscriptions": [
        {
          "InstanceCode": "roommappingredis",
          "EnvCode": "dev"
        },
        {
          "InstanceCode": "hotelmaster",
          "EnvCode": "prod",
          "ConfigurationSection": "MyApp:Databases:HotelMaster"
        }
      ]
    }
  }
}
```