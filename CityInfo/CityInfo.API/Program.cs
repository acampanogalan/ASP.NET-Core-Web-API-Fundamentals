using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args); //Crea el host de la aplicacion

// Add services to the container. SERVICES.

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
}).AddNewtonsoftJson()
.AddXmlDataContractSerializerFormatters(); ; //Suficiente para un API

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

//Instancia del builder para nuestra aplicación
var app = builder.Build();

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


