using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class ApplicationDbContext : DbContext
{
    public DbSet<Person> Persons { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configure the database connection
        string connectionString = "Server=(localdb)\\mssqllocaldb;Database=mytestdb;Trusted_Connection=True;MultipleActiveResultSets=true";
        optionsBuilder.UseSqlServer(connectionString);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder();

        // Add services
        builder.Services.AddDbContext<ApplicationDbContext>();

        var app = builder.Build();

        // InitializeDatabase on app startup
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync(); // Ensure the database is created and migrated
            await SeedInitialData(dbContext); // Seed initial data if necessary
        }

        // Middleware registration and route mapping here
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/", async (HttpContext context) =>
        {
            await context.Response.WriteAsync(File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, "Views", "Home", "Index.cshtml")));
        });

        app.MapGet("/api/users", async (HttpContext context) =>
        {
            using (var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>())
            {
                var users = await dbContext.Persons.ToListAsync();
                return Results.Json(users);
            }
        });

        app.MapGet("/api/users/{id}", async (HttpContext context) =>
        {
            var id = context.Request.RouteValues["id"].ToString();

            using (var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>())
            {
                var user = await dbContext.Persons.FindAsync(id);
                if (user == null)
                {
                    return Results.NotFound();
                }
                return Results.Json(user);
            }
        });

        app.MapPost("/api/users", async (HttpContext context) =>
        {
            using (var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>())
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var newPerson = JsonConvert.DeserializeObject<Person>(requestBody);
                newPerson.Id = Guid.NewGuid().ToString();

                dbContext.Persons.Add(newPerson);
                await dbContext.SaveChangesAsync();

                return Results.Created($"/api/users/{newPerson.Id}", newPerson);
            }
        });


        app.MapDelete("/api/users/{id}", async (HttpContext context) =>
        {
            var id = context.Request.RouteValues["id"].ToString();
            using (var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>())
            {
                var person = await dbContext.Persons.FindAsync(id);
                if (person == null)
                {
                    return Results.NotFound();
                }

                dbContext.Persons.Remove(person);
                await dbContext.SaveChangesAsync();

                return Results.Ok(person);
            }
        });

        app.MapPut("/api/users/{id}", async (HttpContext context) =>
        {
            var id = context.Request.RouteValues["id"].ToString();
            using (var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>())
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var updatedPerson = JsonConvert.DeserializeObject<Person>(requestBody);

                var existingPerson = await dbContext.Persons.FindAsync(id);
                if (existingPerson == null)
                {
                    return Results.NotFound();
                }

                existingPerson.Name = updatedPerson.Name;
                existingPerson.Age = updatedPerson.Age;

                await dbContext.SaveChangesAsync();

                return Results.Ok(existingPerson);
            }
        });

        await app.RunAsync();
    }

    // Seed initial data if necessary
    async static Task SeedInitialData(ApplicationDbContext dbContext)
    {
        if (!await dbContext.Persons.AnyAsync())
        {
            await dbContext.Persons.AddRangeAsync(new List<Person>
            {
                new Person { Id = Guid.NewGuid().ToString(), Name = "Tom", Age = 37 },
                new Person { Id = Guid.NewGuid().ToString(), Name = "Bob", Age = 41 },
                new Person { Id = Guid.NewGuid().ToString(), Name = "Sam", Age = 24 }
            });
            await dbContext.SaveChangesAsync();
        }
    }
}

public class Person
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Age { get; set; }
}
