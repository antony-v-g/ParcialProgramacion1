using System.ComponentModel.DataAnnotations;

namespace ParcialProgramacion1.ViewModels;

public class RegistroSolicitudViewModel
{
    [Required(ErrorMessage = "Debe ingresar el monto solicitado.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }
}