using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infraestructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<Message> Messages => Set<Message>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de la entidad Contact
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.HasKey(c => c.Id);
                // El Id es el ChatId de Telegram, viene del webhook por lo que NO es autoincremental
                entity.Property(c => c.Id)
                      .ValueGeneratedNever();

                entity.Property(c => c.Username).HasMaxLength(100);
                entity.Property(c => c.FirstName).HasMaxLength(150);
                entity.Property(c => c.LastMessagePreview).HasMaxLength(500);
            });

            // Configuración de la entidad Message
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Text).HasMaxLength(4000);

                // Relación 1 a Muchos (Un contacto tiene muchos mensajes) [3.1]
                entity.HasOne(m => m.Contact)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ContactId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
