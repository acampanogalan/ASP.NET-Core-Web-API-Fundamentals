var builder = WebApplication.CreateBuilder(args); //Crea el host de la aplicacion

// Add services to the container. SERVICES.

builder.Services.AddControllers();
// Registra los servicios de la aplicacion swagger necesarios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Instancia del builder para nuestra aplicación
var app = builder.Build();

// Configure the HTTP request pipeline. Construye la inyeccion de dependencias
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); //Middleware controla las peticiones HTTP
    app.UseSwaggerUI(); //MIDDLEWARES
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


