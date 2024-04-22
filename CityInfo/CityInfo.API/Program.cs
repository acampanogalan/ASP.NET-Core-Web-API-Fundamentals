using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CityInfo.API;
using CityInfo.API.DbContexts;
using CityInfo.API.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args); //Crea el host de la aplicacion
//builder.Logging.ClearProviders(); //Limpia los logs
//builder.Logging.AddConsole(); //A�ade el log de consola

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (environment == Environments.Development)
{
    builder.Host.UseSerilog(
        (context, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Debug()
            .WriteTo.Console());
}
else
{
    var secretClient = new SecretClient(
            new Uri("https://democityinfoapikeyvault.vault.azure.net/"),
            new DefaultAzureCredential());
    builder.Configuration.AddAzureKeyVault(secretClient,
        new KeyVaultSecretManager());


    builder.Host.UseSerilog(
        (context, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day)
            .WriteTo.ApplicationInsights(new TelemetryConfiguration
            {
                InstrumentationKey = builder.Configuration["ApplicationInsightsInstrumentationKey"]
            }, TelemetryConverter.Traces));
}


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


builder.Services.AddSingleton<FileExtensionContentTypeProvider>();

#if DEBUG
builder.Services.AddTransient<IMailService, LocalMailService>(); //Inyeccion de dependencias
#else
builder.Services.AddTransient<IMailService, CloudMailService>(); //Inyeccion de dependencias
#endif

builder.Services.AddSingleton<CitiesDataStore>();

builder.Services.AddDbContext<CityInfoContext>(dbContextOptions =>
dbContextOptions.UseSqlServer(builder.Configuration["ConnectionStrings:CityInfoDBConnectionString"]));

builder.Services.AddScoped<ICityInfoRepository, CityInfoRepository>(); //Una por request

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies()); //CurrentAssenbliesm cityInfo.API assembly sera escaneada para ver los profiles

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Authentication:Issuer"],
            ValidAudience = builder.Configuration["Authentication:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration["Authentication:SecretForKey"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MustBeFromAntwerp", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("city", "Antwerp");
    });
});

builder.Services.AddApiVersioning(setupAction =>
{
    setupAction.ReportApiVersions = true;
    setupAction.AssumeDefaultVersionWhenUnspecified = true;
    setupAction.DefaultApiVersion = new ApiVersion(1, 0);
}).AddMvc()
.AddApiExplorer(setupAction =>
{
    setupAction.SubstituteApiVersionInUrl = true;
});

var apiVersionDescriptionProvider = builder.Services.BuildServiceProvider()
    .GetRequiredService<IApiVersionDescriptionProvider>();

builder.Services.AddSwaggerGen(setupAction =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    {
        setupAction.SwaggerDoc(
            $"{description.GroupName}",
            new()
            {
                Title = "City Info API",
                Version = description.ApiVersion.ToString(),
                Description = "Through this API you can access cities and their points of interest."
            });
    }
    var xmlCommentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlCommentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlCommentsFile);

    setupAction.IncludeXmlComments(xmlCommentsFullPath);

    setupAction.AddSecurityDefinition("CityInfoApiBearerAuth", new()
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Input a valid token to acces this API"
    });
    
    setupAction.AddSecurityRequirement(new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "CityInfoApiBearerAuth"
                }
            },
            new List<String>()
        }
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

//Instancia del builder para nuestra aplicaci�n
var app = builder.Build();

// Configure the HTTP request pipeline. Construye la inyeccion de dependencias
if (!app.Environment.IsDevelopment())
{
    //app.UseDeveloperExceptionPage(); //middleware para tratar errores
    app.UseExceptionHandler(); //middleware para tratar errores
}

app.UseForwardedHeaders(); //Asegura que el traspaso a los Forwarded Headers puede consumir los valores del header para el procesamiento

// Configure the HTTP request pipeline. Construye la inyeccion de dependencias
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger(); //Middleware controla las peticiones HTTP
    app.UseSwaggerUI(setupAction =>
    {
        var descriptions = app.DescribeApiVersions();

        foreach (var description in descriptions)
        {
            setupAction.SwaggerEndpoint(
                               $"/swagger/{description.GroupName}/swagger.json",
                                              description.GroupName.ToUpperInvariant());
        }
    }); //MIDDLEWARES
//}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();


