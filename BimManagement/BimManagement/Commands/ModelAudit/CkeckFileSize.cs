using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class CkeckFileSize
    {
        internal static ValidationResult ValidateFileSize(string modelPath)
        {
            var result = new ValidationResult();
            result.IsRelevant = true;

            try
            {
                // Obtener información del archivo
                FileInfo fileInfo = new FileInfo(modelPath);

                // Verificar si el archivo existe
                if (!fileInfo.Exists)
                {
                    result.IsValid = false;
                    result.IsRelevant = false;
                    result.Message = $"El archivo no existe: {modelPath}";
                    return result;
                }

                // Calcular el tamaño en MB
                double fileSizeInMB = fileInfo.Length / (1024.0 * 1024.0);

                // Validar si es menor o igual a 500 MB
                if (fileSizeInMB > 500)
                {
                    result.IsValid = false;
                    result.Message = $"El archivo pesa {fileSizeInMB:F2} MB, se recomienda que no exceda los 500 MB.";
                    return result;
                }

                // Si pasa todas las validaciones
                result.IsValid = true;
                result.Message = "";
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción y devolver un mensaje apropiado
                result.IsValid = false;
                result.IsRelevant = false;
                result.Message = $"Ocurrió un error al validar el archivo: {ex.Message}";
            }

            return result;
        }

    }
}
