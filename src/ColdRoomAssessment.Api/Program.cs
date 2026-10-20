using ColdRoomAssessment.Application;
using ColdRoomAssessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddColdRoomInfrastructure(builder.Configuration);
builder.Services.AddScoped<FacilityService>();
builder.Services.AddScoped<EquipmentTypeService>();
builder.Services.AddScoped<EquipmentService>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
