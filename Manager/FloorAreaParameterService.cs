using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using System;
using System.IO;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Проверяет и при необходимости подключает общий параметр N_Площадь этажа
    /// к категории «Помещения» из корпоративного ФОП.
    /// </summary>
    public static class FloorAreaParameterService
    {
        public const string SharedParameterFilePath =
            @"\\fs-nova\NOVA\04_БИБЛИОТЕКА\BIM\_ФОП\ФОП Новация.txt";

        public static bool EnsureBound(Document doc, Application app, out string error)
        {
            error = null;

            ExternalDefinition definition = FindProjectDefinition(doc, RoomParams.FloorArea);
            if (definition == null)
            {
                if (!TryOpenDefinition(app, out definition, out error))
                    return false;
            }

            if (IsBoundToRooms(doc, definition))
                return true;

            try
            {
                using (var transaction = new Transaction(doc))
                {
                    transaction.Start("TNov - N_Площадь этажа");
                    Logger.Log("Добавляем общий параметр «" + RoomParams.FloorAreaTitle + "» к категории «Помещения»", 1);

                    Category roomCategory = Category.GetCategory(doc, BuiltInCategory.OST_Rooms);
                    BindingMap map = doc.ParameterBindings;

                    if (map.Contains(definition))
                    {
                        ElementBinding existing = map.get_Item(definition) as ElementBinding;
                        if (existing != null && !CategorySetContains(existing.Categories, roomCategory))
                        {
                            CategorySet categories = existing.Categories;
                            categories.Insert(roomCategory);
                            map.ReInsert(definition, existing);
                        }
                    }
                    else
                    {
                        CategorySet categories = app.Create.NewCategorySet();
                        categories.Insert(roomCategory);
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        InsertBinding(map, definition, binding);
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                error = "Не удалось добавить параметр «" + RoomParams.FloorAreaTitle + "» к категории «Помещения»: "
                        + ex.Message;
                Logger.Log(error, 4);
                return false;
            }

            return true;
        }

        private static ExternalDefinition FindProjectDefinition(Document doc, Guid paramGuid)
        {
            BindingMap map = doc.ParameterBindings;
            DefinitionBindingMapIterator iterator = map.ForwardIterator();
            while (iterator.MoveNext())
            {
                if (iterator.Key is ExternalDefinition external && external.GUID == paramGuid)
                    return external;
            }
            return null;
        }

        private static bool IsBoundToRooms(Document doc, Definition definition)
        {
            BindingMap map = doc.ParameterBindings;
            if (!map.Contains(definition)) return false;

            ElementBinding binding = map.get_Item(definition) as ElementBinding;
            if (binding == null) return false;

            Category roomCategory = Category.GetCategory(doc, BuiltInCategory.OST_Rooms);
            return CategorySetContains(binding.Categories, roomCategory);
        }

        private static bool CategorySetContains(CategorySet categories, Category category)
        {
            if (categories == null || category == null) return false;
            foreach (Category item in categories)
            {
                if (item != null && item.Id == category.Id)
                    return true;
            }
            return false;
        }

        private static bool TryOpenDefinition(Application app, out ExternalDefinition definition, out string error)
        {
            definition = null;
            error = null;

            DefinitionFile file = app.OpenSharedParameterFile();
            definition = FindDefinitionInFile(file, RoomParams.FloorArea);
            if (definition != null)
                return true;

            if (!File.Exists(SharedParameterFilePath))
            {
                error = "Файл общих параметров недоступен:\n" + SharedParameterFilePath
                        + "\n\nПараметр «" + RoomParams.FloorAreaTitle + "» не найден в подключённом ФОП.";
                Logger.Log(error, 4);
                return false;
            }

            try
            {
                app.SharedParametersFilename = SharedParameterFilePath;
                file = app.OpenSharedParameterFile();
            }
            catch (Exception ex)
            {
                error = "Не удалось подключить файл общих параметров:\n" + SharedParameterFilePath
                        + "\n\n" + ex.Message;
                Logger.Log(error, 4);
                return false;
            }

            definition = FindDefinitionInFile(file, RoomParams.FloorArea);
            if (definition != null)
                return true;

            error = "В файле общих параметров не найден параметр «" + RoomParams.FloorAreaTitle + "» ("
                    + RoomParams.FloorArea + ").\n\nПроверьте ФОП:\n" + SharedParameterFilePath;
            Logger.Log(error, 4);
            return false;
        }

        private static ExternalDefinition FindDefinitionInFile(DefinitionFile file, Guid paramGuid)
        {
            if (file == null) return null;

            foreach (DefinitionGroup group in file.Groups)
            {
                foreach (Definition item in group.Definitions)
                {
                    if (item is ExternalDefinition external && external.GUID == paramGuid)
                        return external;
                }
            }
            return null;
        }

        private static void InsertBinding(BindingMap map, ExternalDefinition definition, InstanceBinding binding)
        {
#if R2022
            map.Insert(definition, binding, BuiltInParameterGroup.PG_DATA);
#else
            map.Insert(definition, binding, GroupTypeId.Data);
#endif
        }
    }
}
