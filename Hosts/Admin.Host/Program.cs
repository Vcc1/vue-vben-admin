using Framework.Core.IOC;
using HotelMatchingPipeline.Api;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.Infra;
using Infrastructure.Sql.RoomMapping;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddHotelMatchingApplication(builder.Configuration);
builder.Services.AddHotelMatchingInfrastructure(builder.Configuration);
builder.Services.AddBusiness([typeof(RoomMappingDbContext).Assembly]);
builder.Services.AddControllers().AddHotelMatchingApi();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
