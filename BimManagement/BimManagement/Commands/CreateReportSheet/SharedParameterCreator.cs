using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;

namespace BimManagement
{
    /// <summary>
    /// Crea los parámetros compartidos CSL-Unidad y CSL-Cantidad
    /// en todas las categorías modelables de Revit.
    /// </summary>
    public class SharedParameterCreator
    {
        private readonly Document _doc;
        private readonly AppConfig _config;

        // Nombre del grupo dentro del archivo de parámetros compartidos
        private const string ParamGroupName = "CSL-Parametros";
        private const string ParamUnidad = "CSL-Unidad";
        private const string ParamCantidad = "CSL-Cantidad";

        // GUIDs fijos para garantizar consistencia entre sesiones
        private static readonly Guid GuidUnidad   = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        private static readonly Guid GuidCantidad = new Guid("b2c3d4e5-f6a7-8901-bcde-f12345678901");

        public SharedParameterCreator(Document doc, AppConfig config)
        {
            _doc = doc;
            _config = config;
        }

        // Categorías omitidas en la última ejecución (para diagnóstico)
        public List<string> SkippedCategories { get; } = new List<string>();

        public void EnsureParametersExist()
        {
            Autodesk.Revit.ApplicationServices.Application app = _doc.Application;

            // Guardar referencia al archivo de parámetros compartidos actual
            string originalSharedParamFile = app.SharedParametersFilename;

            // Ruta del archivo de parámetros compartidos de CESEL
            string sharedParamPath = _config.SharedParameterFilePath;

            // Si no existe el archivo, crearlo con la estructura mínima
            EnsureSharedParamFileExists(sharedParamPath);

            app.SharedParametersFilename = sharedParamPath;
            DefinitionFile defFile = app.OpenSharedParameterFile();

            if (defFile == null)
                throw new InvalidOperationException($"No se pudo abrir el archivo de parámetros compartidos: {sharedParamPath}");

            // Obtener o crear el grupo
            DefinitionGroup group = defFile.Groups.get_Item(ParamGroupName)
                                    ?? defFile.Groups.Create(ParamGroupName);

            // Definir los dos parámetros
            ExternalDefinition defUnidad = GetOrCreateDefinition(group, ParamUnidad, SpecTypeId.String.Text);
            ExternalDefinition defCantidad = GetOrCreateDefinition(group, ParamCantidad, SpecTypeId.Number);

            // Categorías modelables
            CategorySet catSet = BuildModelableCategorySet();

            // Binding tipo instancia
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);

            // Agregar parámetros si no existen
            BindParameterIfNeeded(_doc, defUnidad, instanceBinding, BuiltInParameterGroup.PG_IDENTITY_DATA);
            BindParameterIfNeeded(_doc, defCantidad, instanceBinding, BuiltInParameterGroup.PG_IDENTITY_DATA);

            // Restaurar archivo original
            if (!string.IsNullOrEmpty(originalSharedParamFile))
                app.SharedParametersFilename = originalSharedParamFile;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void EnsureSharedParamFileExists(string path)
        {
            // Siempre sobreescribir: GUIDs son fijos, así se garantiza
            // que USERMODIFIABLE quede en 0 aunque el archivo ya existiera.
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Archivo de parámetros compartidos con formato estándar de Revit
            // Los GUIDs son fijos para garantizar consistencia si el archivo se recrea
            string content =
                "# This is a Revit shared parameter file.\r\n" +
                "# Do not edit manually.\r\n" +
                "*META\tVERSION\tMINVERSION\r\n" +
                "META\t2\t1\r\n" +
                "*GROUP\tID\tNAME\r\n" +
                $"GROUP\t1\t{ParamGroupName}\r\n" +
                "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE\r\n" +
                $"PARAM\t{GuidUnidad:D}\t{ParamUnidad}\tTEXT\t\t1\t1\tUnidad de medida CSL\t0\t0\r\n" +
                $"PARAM\t{GuidCantidad:D}\t{ParamCantidad}\tNUMBER\t\t1\t1\tCantidad CSL\t0\t0\r\n";

            File.WriteAllText(path, content);
        }

        private ExternalDefinition GetOrCreateDefinition(DefinitionGroup group, string name, ForgeTypeId specTypeId)
        {
            ExternalDefinition existing = group.Definitions.get_Item(name) as ExternalDefinition;
            if (existing != null) return existing;

            var options = new ExternalDefinitionCreationOptions(name, specTypeId)
            {
                UserModifiable = false,
                Visible = true
            };
            return group.Definitions.Create(options) as ExternalDefinition;
        }

        private void BindParameterIfNeeded(Document doc, ExternalDefinition def,
            InstanceBinding newBinding, BuiltInParameterGroup paramGroup)
        {
            BindingMap bindingMap = doc.ParameterBindings;

            // Intentar insertar; si ya existe (mismo GUID) Insert devuelve false
            if (bindingMap.Insert(def, newBinding, paramGroup))
                return; // Primera vez, OK

            // Ya estaba vinculado → actualizar categorías con ReInsert
            // Esto también cubre el caso en que se agregaron categorías nuevas
            bindingMap.ReInsert(def, newBinding, paramGroup);
        }

        private CategorySet BuildModelableCategorySet()
        {
            Autodesk.Revit.ApplicationServices.Application app = _doc.Application;
            CategorySet catSet = app.Create.NewCategorySet();

            // Lista de categorías modelables principales
            var builtInCats = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_Ramps,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_Furniture,
                BuiltInCategory.OST_FurnitureSystems,
                BuiltInCategory.OST_Casework,
                BuiltInCategory.OST_Railings,
                BuiltInCategory.OST_StairsRailing,
                BuiltInCategory.OST_Topography,
                BuiltInCategory.OST_Site,
                BuiltInCategory.OST_Parking,
                BuiltInCategory.OST_Roads,
                BuiltInCategory.OST_Planting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_ElectricalFixtures,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_StructuralStiffener,
                BuiltInCategory.OST_Entourage,
                BuiltInCategory.OST_Mass,
                BuiltInCategory.OST_CurtainWallMullions,
                BuiltInCategory.OST_CurtainWallPanels,
                BuiltInCategory.OST_SpecialityEquipment,
                BuiltInCategory.OST_Parts
            };

            foreach (BuiltInCategory bic in builtInCats)
            {
                try
                {
                    Category cat = Category.GetCategory(_doc, bic);
                    if (cat == null)
                    {
                        SkippedCategories.Add($"{bic}: null (no disponible en este modelo)");
                        continue;
                    }
                    if (!cat.AllowsBoundParameters)
                    {
                        SkippedCategories.Add($"{cat.Name}: AllowsBoundParameters = false");
                        continue;
                    }
                    catSet.Insert(cat);
                }
                catch (Exception ex)
                {
                    SkippedCategories.Add($"{bic}: excepción – {ex.Message}");
                }
            }

            return catSet;
        }

    }
}