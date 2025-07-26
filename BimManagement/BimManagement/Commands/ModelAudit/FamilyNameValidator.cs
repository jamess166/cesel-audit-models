using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace BimManagement.Commands.ModelAudit
{
    public class FamilyNameValidator
    {
        private const int MaxNameLength = 60;
        private const string UpperCamelCasePattern = @"^[A-Z][a-zA-Z0-9\-._]*$";

        //private const string UpperCamelCasePattern = @"^[A-Z][a-zA-Z0-9-]*$";

        internal ValidationResult ValidateFamilies(Document doc)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Message = "",
                IsRelevant = true
            };

            var invalidFamilyNames = new List<string>();
            var invalidFamilyTypes = new List<string>();

            var collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
            foreach (Family family in collector)
            {
                // Solo validar familias recargables (que permiten cambiar nombre)
                bool isLoadableFamily = family.IsEditable;

                if (isLoadableFamily)
                {
                    // Validar nombre de la familia recargable
                    if (!IsValidFamilyName(family.Name))
                    {
                        invalidFamilyNames.Add(family.Name);
                    }

                    // Validar tipos de familia recargable
                    foreach (ElementId typeId in family.GetFamilySymbolIds())
                    {
                        var familyType = doc.GetElement(typeId) as FamilySymbol;
                        if (familyType != null && !IsValidFamilyType(family.Name, familyType.Name))
                        {
                            invalidFamilyTypes.Add($"{family.Name}: {familyType.Name}");
                        }
                    }
                }
            }

            if (invalidFamilyNames.Any())
            {
                result.Message += "Nombres de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyNames) + "\n\n";
                result.IsValid = false;
            }

            if (invalidFamilyTypes.Any())
            {
                result.Message += "Tipos de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyTypes) + "\n\n";
                result.IsValid = false;
            }

            if (result.IsValid)
            {
                result.Message = "";
            }

            return result;
        }

        private bool IsValidFamilyName(string familyName)
        {
            // Validar que no sea nulo o vacío
            if (string.IsNullOrEmpty(familyName))
            {
                return false;
            }

            // Validar longitud máxima
            if (familyName.Length > MaxNameLength)
            {
                return false;
            }

            // Validar que comience con CPA
            if (!familyName.StartsWith("CPA"))
            {
                return false;
            }

            // Validar formato UpperCamelCase con guiones permitidos
            // Solo caracteres alfanuméricos y guiones, comenzando con mayúscula
            if (!Regex.IsMatch(familyName, UpperCamelCasePattern))
            {
                return false;
            }

            return true;
        }

        private bool IsValidFamilyType(string familyName, string typeName)
        {
            // Validar que no sea nulo o vacío
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            // Validar longitud máxima
            if (typeName.Length > MaxNameLength)
            {
                return false;
            }

            // Validar que comience con el nombre completo de la familia
            if (!typeName.StartsWith(familyName))
            {
                return false;
            }

            // Validar formato UpperCamelCase con guiones permitidos
            if (!Regex.IsMatch(typeName, UpperCamelCasePattern))
            {
                return false;
            }

            return true;
        }
    }

    //public class FamilyNameValidator
    //{
    //    private const int MaxNameLength = 60;
    //    private const string UpperCamelCasePattern = @"^[A-Z][a-zA-Z0-9]*$";
    //    private const string UpperAndCamelCasePattern = @"^([A-Z][a-zA-Z0-9]*|[A-Z]+)$";
    //    private const string TypeDescriptionPattern = @"^[A-Za-z0-9\.\-_]+$";

    //    internal ValidationResult ValidateFamilies(Document doc)
    //    {
    //        var result = new ValidationResult
    //        {
    //            IsValid = true,
    //            Message = "",
    //            IsRelevant = true
    //        };

    //        var invalidFamilyNames = new List<string>();
    //        var invalidFamilyTypes = new List<string>();
    //        var invalidSystemFamilyTypes = new List<string>();

    //        var collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
    //        foreach (Family family in collector)
    //        {
    //            // Una familia es recargable si tiene un archivo .rfa asociado
    //            bool isLoadableFamily = family.IsEditable;

    //            if (isLoadableFamily)
    //            {
    //                // Validar nombre de la familia recargable
    //                if (!IsValidFamilyName(family.Name))
    //                {
    //                    invalidFamilyNames.Add(family.Name);
    //                }


    //                // Validar tipos de familia recargable
    //                foreach (ElementId typeId in family.GetFamilySymbolIds())
    //                {
    //                    var familyType = doc.GetElement(typeId) as FamilySymbol;
    //                    if (familyType != null && !IsValidFamilyType(family.Name, familyType.Name))
    //                    {
    //                        invalidFamilyTypes.Add($"{family.Name}: {familyType.Name}");
    //                    }
    //                }
    //            }
    //            else
    //            {
    //                // Para familias de sistema, solo validar los tipos
    //                foreach (ElementId typeId in family.GetFamilySymbolIds())
    //                {
    //                    var familyType = doc.GetElement(typeId) as FamilySymbol;
    //                    if (familyType != null && !IsValidSystemFamilyType(familyType.Name))
    //                    {
    //                        invalidSystemFamilyTypes.Add($"{family.Name}: {familyType.Name}");
    //                    }
    //                }
    //            }
    //        }

    //        if (invalidFamilyNames.Any())
    //        {
    //            result.Message += "Nombre de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyNames) + "\n\n";
    //            result.IsValid = false;
    //        }

    //        if (invalidFamilyTypes.Any())
    //        {
    //            result.Message += "Tipos de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyTypes) + "\n\n";
    //            result.IsValid = false;
    //        }

    //        if (invalidSystemFamilyTypes.Any())
    //        {
    //            result.Message += "Tipos de familias de sistema inválidos:\n" + string.Join("\n", invalidSystemFamilyTypes) + "\n";
    //            result.IsValid = false;
    //        }

    //        if (result.IsValid)
    //        {
    //            result.Message = "";
    //        }

    //        return result;
    //    }

    //    private bool IsValidFamilyName(string familyName)
    //    {
    //        if (string.IsNullOrEmpty(familyName) || familyName.Length > MaxNameLength)
    //        {
    //            return false;
    //        }

    //        var parts = familyName.Split('-');
    //        if (parts.Length != 2) return false;

    //        return parts[0] == "CPA" && Regex.IsMatch(parts[1], UpperAndCamelCasePattern);
    //    }

    //    private bool IsValidFamilyType(string familyName, string typeName)
    //    {
    //        if (string.IsNullOrEmpty(typeName) || typeName.Length > MaxNameLength)
    //        {
    //            return false;
    //        }

    //        // El tipo debe comenzar exactamente con el nombre de la familia
    //        if (!typeName.StartsWith(familyName))
    //        {
    //            return false;
    //        }

    //        // Verificar que después del nombre de la familia viene un guión
    //        if (typeName.Length <= familyName.Length || typeName[familyName.Length] != '-')
    //        {
    //            return false;
    //        }

    //        // Obtener la parte después del nombre de la familia
    //        string additionalDescription = typeName.Substring(familyName.Length + 1);

    //        // Dividir la descripción adicional por guión bajo
    //        string[] descriptionParts = additionalDescription.Split('_');
    //        if (descriptionParts.Length > 2) return false;

    //        // Validar cada parte de la descripción adicional
    //        foreach (var part in descriptionParts)
    //        {
    //            if (string.IsNullOrEmpty(part) || !Regex.IsMatch(part, TypeDescriptionPattern))
    //            {
    //                return false;
    //            }
    //        }

    //        return true;
    //    }

    //    private bool IsValidSystemFamilyType(string typeName)
    //    {
    //        if (string.IsNullOrEmpty(typeName) || typeName.Length > MaxNameLength)
    //        {
    //            return false;
    //        }

    //        string[] mainAndCode = typeName.Split('_');
    //        if (mainAndCode.Length > 2) return false;

    //        string[] parts = mainAndCode[0].Split('-');
    //        if (parts.Length < 3 || parts.Length > 4) return false;

    //        if (parts[0] != "CPA") return false;

    //        for (int i = 1; i < parts.Length; i++)
    //        {
    //            if (!Regex.IsMatch(parts[i], TypeDescriptionPattern))
    //            {
    //                return false;
    //            }
    //        }

    //        if (mainAndCode.Length == 2 && !Regex.IsMatch(mainAndCode[1], TypeDescriptionPattern))
    //        {
    //            return false;
    //        }

    //        return true;
    //    }
    //}
}

//namespace BimManagement.Commands.ModelAudit
//{
//    public class FamilyNameValidator
//    {
//        private const int MaxNameLength = 60;
//        // Mantener patrón estricto para nombres de familias
//        private const string UpperCamelCasePattern = @"^[A-Z][a-zA-Z0-9]*$";
//        private const string UpperAndCamelCasePattern = @"^([A-Z][a-zA-Z0-9]*|[A-Z]+)$";
//        // Nuevo patrón flexible solo para tipos
//        private const string TypeDescriptionPattern = @"^[A-Za-z0-9\.\-_]+$";

//        internal ValidationResult ValidateFamilies(Document doc)
//        {
//            var result = new ValidationResult
//            {
//                IsValid = true,
//                Message = "",
//                IsRelevant = true
//            };

//            var invalidFamilyNames = new List<string>();
//            var invalidFamilyTypes = new List<string>();
//            var invalidSystemFamilyTypes = new List<string>();

//            // Obtener todas las familias en el documento
//            var collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
//            foreach (Family family in collector)
//            {
//                // Una familia es recargable si tiene un archivo .rfa asociado
//                bool isLoadableFamily = family.IsEditable;

//                if (isLoadableFamily)
//                {
//                    // Validar nombre de la familia recargable
//                    if (!IsValidFamilyName(family.Name))
//                    {
//                        invalidFamilyNames.Add(family.Name);
//                    }

//                    // Validar tipos de familia recargable
//                    foreach (ElementId typeId in family.GetFamilySymbolIds())
//                    {
//                        var familyType = doc.GetElement(typeId) as FamilySymbol;
//                        if (familyType != null && !IsValidFamilyType(family.Name, familyType.Name))
//                        {
//                            invalidFamilyTypes.Add($"{family.Name}: {familyType.Name}");
//                        }
//                    }
//                }
//                else
//                {
//                    // Para familias de sistema, solo validamos sus tipos
//                    var familySymbols = new FilteredElementCollector(doc)
//                        .OfClass(typeof(FamilySymbol))
//                        .WhereElementIsElementType()
//                        .Cast<FamilySymbol>()
//                        .Where(fs => fs.Family.Id == family.Id); // Usar Id en lugar de Name para la comparación

//                    foreach (var familyType in familySymbols)
//                    {
//                        if (!IsValidSystemFamilyType(familyType.Name))
//                        {
//                            invalidSystemFamilyTypes.Add($"{family.Name}: {familyType.Name}");
//                        }
//                    }
//                }
//            }

//            // Construir mensaje de resultado
//            if (invalidFamilyNames.Any())
//            {
//                result.Message += "Nombres de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyNames) + "\n\n";
//                result.IsValid = false;
//            }

//            if (invalidFamilyTypes.Any())
//            {
//                result.Message += "Tipos de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyTypes) + "\n\n";
//                result.IsValid = false;
//            }

//            if (invalidSystemFamilyTypes.Any())
//            {
//                result.Message += "Tipos de familias de sistema inválidos:\n" + string.Join("\n", invalidSystemFamilyTypes);
//                result.IsValid = false;
//            }

//            if (result.IsValid)
//            {
//                result.Message = "";
//            }

//            return result;
//        }

//        // Los métodos de validación permanecen iguales
//        private bool IsValidFamilyName(string familyName)
//        {
//            if (string.IsNullOrEmpty(familyName) || familyName.Length > MaxNameLength)
//            {
//                return false;
//            }

//            var parts = familyName.Split('-');
//            if (parts.Length != 2) return false;

//            return parts[0] == "CPA" && Regex.IsMatch(parts[1], UpperAndCamelCasePattern);
//        }

//        private bool IsValidFamilyType(string familyName, string typeName)
//        {
//            if (string.IsNullOrEmpty(typeName) || typeName.Length > MaxNameLength)
//            {
//                return false;
//            }

//            return typeName.StartsWith(familyName + "-") && Regex.IsMatch(typeName.Substring(familyName.Length + 1), UpperCamelCasePattern);
//        }

//        private bool IsValidSystemFamilyType(string typeName)
//        {
//            if (string.IsNullOrEmpty(typeName) || typeName.Length > MaxNameLength)
//            {
//                return false;
//            }

//            var parts = typeName.Split('-');
//            if (parts.Length < 3 || parts.Length > 4) return false;

//            return parts[0] == "CPA"
//                && Regex.IsMatch(parts[1], UpperCamelCasePattern)
//                && Regex.IsMatch(parts[2], UpperCamelCasePattern)
//                && (parts.Length == 3 || Regex.IsMatch(parts[3], UpperCamelCasePattern));
//        }
//    }
//}

//namespace BimManagement.Commands.ModelAudit
//{
//    public class FamilyNameValidator
//    {
//        private const int MaxNameLength = 60; // Longitud máxima del nombre
//        private const string UpperCamelCasePattern = @"^[A-Z][a-zA-Z0-9]*$";
//        private const string UpperAndCamelCasePattern = @"^([A-Z][a-zA-Z0-9]*|[A-Z]+)$";

//        internal ValidationResult ValidateFamilies(Document doc)
//        {
//            var result = new ValidationResult
//            {
//                IsValid = true,
//                Message = "",
//                IsRelevant = true
//            };

//            // Listas para agrupar resultados
//            var invalidFamilyNames = new List<string>();
//            var invalidFamilyTypes = new List<string>();
//            var invalidSystemFamilyTypes = new List<string>();

//            // Obtener todas las familias en el documento
//            var collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
//            foreach (Family family in collector)
//            {
//                // Verificar si la familia es recargable
//                bool isLoadableFamily = family.FamilyCategory != null;

//                if (isLoadableFamily)
//                {
//                    // Validar si el nombre de la familia puede modificarse
//                    bool isFamilyNameEditable = IsFamilyNameEditable(family);

//                    if (isFamilyNameEditable)
//                    {
//                        // Validar nombre de la familia
//                        if (!IsValidFamilyName(family.Name))
//                        {
//                            invalidFamilyNames.Add(family.Name);
//                        }
//                    }

//                    // Validar tipos de familia recargable
//                    foreach (ElementId typeId in family.GetFamilySymbolIds())
//                    {
//                        var familyType = doc.GetElement(typeId) as FamilySymbol;
//                        if (familyType != null && !IsValidFamilyType(family.Name, familyType.Name))
//                        {
//                            invalidFamilyTypes.Add(familyType.Name);
//                        }
//                    }
//                }
//                else
//                {
//                    // Validar tipos de familia de sistema
//                    var familySymbols = new FilteredElementCollector(doc)
//                        .OfClass(typeof(FamilySymbol))
//                        .WhereElementIsElementType()
//                        .Cast<FamilySymbol>()
//                        .Where(fs => fs.Family.Name == family.Name);

//                    foreach (var familyType in familySymbols)
//                    {
//                        if (!IsValidSystemFamilyType(familyType.Name))
//                        {
//                            invalidSystemFamilyTypes.Add(familyType.Name);
//                        }
//                    }
//                }
//            }

//            // Construir mensaje de resultado
//            if (invalidFamilyNames.Any())
//            {
//                result.Message += "Nombre de familias inválidos:\n" + string.Join("\n", invalidFamilyNames) + "\n";
//                result.IsValid = false;
//            }

//            if (invalidFamilyTypes.Any())
//            {
//                result.Message += "Tipos de familias recargables inválidos:\n" + string.Join("\n", invalidFamilyTypes) + "\n";
//                result.IsValid = false;
//            }

//            if (invalidSystemFamilyTypes.Any())
//            {
//                result.Message += "Tipos de familias de sistema inválidos:\n" + string.Join("\n", invalidSystemFamilyTypes) + "\n";
//                result.IsValid = false;
//            }

//            // Si no hay errores, limpiar el mensaje
//            if (result.IsValid)
//            {
//                result.Message = "";
//            }

//            return result;
//        }

//        // Validar si el nombre de la familia puede editarse
//        private bool IsFamilyNameEditable(Family family)
//        {
//            try
//            {
//                // Intentar acceder al parámetro del nombre de la familia
//                Parameter param = family.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
//                return param != null && !param.IsReadOnly;
//            }
//            catch
//            {
//                // Si no se puede acceder, asumimos que no es editable
//                return false;
//            }
//        }

//        // Validar el nombre de una familia recargable
//        private bool IsValidFamilyName(string familyName)
//        {
//            if (string.IsNullOrEmpty(familyName) || familyName.Length > 60)
//            {
//                return false;
//            }

//            var parts = familyName.Split('-');
//            if (parts.Length != 2) return false;

//            return parts[0] == "CPA" && Regex.IsMatch(parts[1], UpperAndCamelCasePattern);
//        }

//        // Validar el nombre de un tipo en una familia recargable
//        private bool IsValidFamilyType(string familyName, string typeName)
//        {
//            if (string.IsNullOrEmpty(typeName) || typeName.Length > 60)
//            {
//                return false;
//            }

//            // Regla: [Nombre de familia]-[Detalles del tipo]
//            return typeName.StartsWith(familyName + "-") && Regex.IsMatch(typeName.Substring(familyName.Length + 1), UpperCamelCasePattern);
//        }

//        // Validar el nombre de un tipo en una familia de sistema
//        private bool IsValidSystemFamilyType(string typeName)
//        {
//            if (string.IsNullOrEmpty(typeName) || typeName.Length > 60)
//            {
//                return false;
//            }

//            // Regla: Autor-Descripción-Formato/Sistema[-Tipo de Marca (Opcional)]
//            var parts = typeName.Split('-');
//            if (parts.Length < 3 || parts.Length > 4) return false;

//            return parts[0] == "CPA"
//                && Regex.IsMatch(parts[1], UpperCamelCasePattern)
//                && Regex.IsMatch(parts[2], UpperCamelCasePattern)
//                && (parts.Length == 3 || Regex.IsMatch(parts[3], UpperCamelCasePattern));
//        }       
//    }
//}
