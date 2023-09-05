using TestWebApiGenerator;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServicesFromConfiguration();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "hello");

app.Run();
public partial class Program { }
