using CityInfo.API;
using CityInfo.API.DbContexts;
using CityInfo.API.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args); //Crea el host de la aplicacion
//builder.Logging.ClearProviders(); //Limpia los logs
//builder.Logging.AddConsole(); //Añade el log de consola
builder.Host.UseSerilog();


// Add services to the container. SERVICES.

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
}).AddNewtonsoftJson()
.AddXmlDataContractSerializerFormatters(); ; //Suficiente para un API

builder.Services.AddProblemDetails(); //Middleware para tratar errores
//builder.Services.AddProblemDetails(options =>
//{
//    options.CustomizeProblemDetails = ctx =>
//    {
//        ctx.ProblemDetails.Extensions.Add("additionalInfo",
//            "Additional info example");
//        ctx.ProblemDetails.Extensions.Add("server", 
//            Environment.MachineName);
//    };
//});

// Registra los servicios de la aplicacion swagger necesarios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<FileExtensionContentTypeProvider>();

#if DEBUG
builder.Services.AddTransient<IMailService, LocalMailService>(); //Inyeccion de dependencias
#else
builder.Services.AddTransient<IMailService, CloudMailService>(); //Inyeccion de dependencias
#endif

builder.Services.AddSingleton<CitiesDataStore>();

builder.Services.AddDbContext<CityInfoContext>(dbContextOptions =>
dbContextOptions.UseSqlServer(builder.Configuration["ConnectionStrings:CityInfoDBConnectionString"]));
//"Server=localhost\\SQLEXPRESS;Database=Agenda;Trusted_Connection=True;"

builder.Services.AddScoped<ICityInfoRepository, CityInfoRepository>(); //Una por request

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies()); //CurrentAssenbliesm cityInfo.API assembly sera escaneada para ver los profiles

//Instancia del builder para nuestra aplicación
var app = builder.Build();

// Configure the HTTP request pipeline. Construye la inyeccion de dependencias
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(); //middleware para tratar errores
}

// Configure the HTTP request pipeline. Construye la inyeccion de dependencias
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); //Middleware controla las peticiones HTTP
    app.UseSwaggerUI(); //MIDDLEWARES
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();


