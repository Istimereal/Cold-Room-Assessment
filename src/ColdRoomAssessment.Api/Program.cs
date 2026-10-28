using System.Text.Json.Serialization;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Imports;
using ColdRoomAssessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddColdRoomInfrastructure(builder.Configuration);
builder.Services.AddScoped<FacilityService>();
builder.Services.AddScoped<EquipmentTypeService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<MeasurementImportService>();
builder.Services.AddScoped<FacilityCoverageService>();
builder.Services.AddScoped<AnalysisPeriodService>();
builder.Services.AddScoped<AnalysisDataService>();
builder.Services.AddScoped<AnalysisService>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
