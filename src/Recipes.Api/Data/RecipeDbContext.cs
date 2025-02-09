using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recipes.Api.Models;

namespace Recipes.Api.Data;

public class RecipeDbContext(DbContextOptions<RecipeDbContext> options)
    : IdentityDbContext<RecipeApiUserDataModel>(options)
{
    public DbSet<RecipeDataModel> Recipes { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<RecipeDataModel>(m =>
        {
            m.ToTable("Recipes");
            m.HasKey(p => p.Id);
            m.Property(p => p.Id).HasMaxLength(128);
            m.Property(p => p.Name).HasMaxLength(256);
            m.Property(p => p.Description).HasMaxLength(512);
            m.Property(p => p.LookupCode).HasMaxLength(32);
            m.Property(e => e.DateCreated).HasDefaultValueSql("now() at time zone 'utc'");
            m .OwnsMany(p => p.Ingredients, o =>
            {
                o.ToTable("Ingredients");
                o.Property(p => p.Name).HasMaxLength(128);
                o.Property(p =>p.Unit).HasMaxLength(32);
            });
        });

        modelBuilder.Entity<RecipeApiUserDataModel>(m =>
        {
            m.Property(p => p.StripeCustomerId).HasMaxLength(128);
        });
    }
}

public class RecipeApiUserDataModel : IdentityUser
{
    public required string StripeCustomerId { get; set; }
}