using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class RiddleCsvImporterWindow : EditorWindow
{
    private string csvPath = "";

    private string targetFolder =
        "Assets/_Project/Data/Riddles";

    private string databasePath =
        "Assets/_Project/Data/RiddleDatabase.asset";

    private RiddleDatabaseSO database;

    [MenuItem("Tools/Guess It/CSV Importer")]
    public static void Open()
    {
        GetWindow<RiddleCsvImporterWindow>("CSV Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "Guess It CSV Importer",
            EditorStyles.boldLabel
        );

        GUILayout.Space(10);

        GUILayout.Label(
            "Archivo de origen",
            EditorStyles.boldLabel
        );

        GUILayout.BeginHorizontal();

        csvPath = EditorGUILayout.TextField(
            "CSV Path",
            csvPath
        );

        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            string selectedPath = EditorUtility.OpenFilePanel(
                "Seleccionar CSV",
                "",
                "csv"
            );

            if (!string.IsNullOrEmpty(selectedPath))
            {
                csvPath = selectedPath;
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label(
            "Destino",
            EditorStyles.boldLabel
        );

        targetFolder = EditorGUILayout.TextField(
            "Riddles Folder",
            targetFolder
        );

        databasePath = EditorGUILayout.TextField(
            "Database Path",
            databasePath
        );

        database = (RiddleDatabaseSO)EditorGUILayout.ObjectField(
            "Database",
            database,
            typeof(RiddleDatabaseSO),
            false
        );

        GUILayout.Space(15);

        if (GUILayout.Button(
            "Import CSV",
            GUILayout.Height(32)
        ))
        {
            ImportCsv();
        }

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "El CSV debe contener estas columnas:\n" +
            "id, category, difficulty, answer, acceptedAnswers, " +
            "hint1, hint2, hint3, hint4, hint5",
            MessageType.Info
        );
    }

    private void ImportCsv()
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            Debug.LogError(
                "No seleccionaste ningún archivo CSV."
            );

            return;
        }

        if (!File.Exists(csvPath))
        {
            Debug.LogError(
                "El archivo CSV seleccionado no existe."
            );

            return;
        }

        if (
            string.IsNullOrWhiteSpace(targetFolder) ||
            !targetFolder.StartsWith("Assets")
        )
        {
            Debug.LogError(
                "La carpeta de acertijos debe estar dentro de Assets."
            );

            return;
        }

        if (
            string.IsNullOrWhiteSpace(databasePath) ||
            !databasePath.StartsWith("Assets")
        )
        {
            Debug.LogError(
                "La base de datos debe estar dentro de Assets."
            );

            return;
        }

        try
        {
            string csvText = File.ReadAllText(
                csvPath,
                Encoding.UTF8
            );

            List<List<string>> table = ParseCsv(csvText);

            if (table.Count < 2)
            {
                Debug.LogError(
                    "El CSV debe contener una fila de encabezados " +
                    "y al menos un acertijo."
                );

                return;
            }

            Debug.Log($"Filas encontradas: {table.Count}");

            List<RiddleImportData> importedData =
                new List<RiddleImportData>();

            HashSet<string> idsInCsv =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            // La fila 0 contiene los encabezados.
            for (int i = 1; i < table.Count; i++)
            {
                List<string> row = table[i];

                if (IsEmptyRow(row))
                {
                    continue;
                }

                RiddleImportData importedRiddle =
                    CreateImportData(row, i + 1);

                if (importedRiddle == null)
                {
                    continue;
                }

                if (!idsInCsv.Add(importedRiddle.id))
                {
                    Debug.LogError(
                        $"Fila {i + 1}: el ID " +
                        $"'{importedRiddle.id}' está duplicado."
                    );

                    return;
                }

                importedData.Add(importedRiddle);
            }

            if (importedData.Count == 0)
            {
                Debug.LogError(
                    "No se encontró ningún acertijo válido."
                );

                return;
            }

            Debug.Log(
                $"Importadas {importedData.Count} adivinanzas."
            );

            EnsureFolder(targetFolder);

            if (database == null)
            {
                database = LoadOrCreateDatabase();
            }

            if (database == null)
            {
                Debug.LogError(
                    "No se pudo cargar o crear la base de datos."
                );

                return;
            }

            int createdCount = 0;
            int updatedCount = 0;

            List<RiddleSO> importedAssets =
                new List<RiddleSO>();

            foreach (RiddleImportData importData in importedData)
            {
                RiddleSO riddleAsset =
                    CreateOrUpdateRiddleAsset(
                        importData,
                        ref createdCount,
                        ref updatedCount
                    );

                if (riddleAsset != null)
                {
                    importedAssets.Add(riddleAsset);
                }
            }

            UpdateDatabase(importedAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string result =
                $"Importación completada.\n\n" +
                $"Creados: {createdCount}\n" +
                $"Actualizados: {updatedCount}\n" +
                $"Total en la base: {importedAssets.Count}";

            Debug.Log(result);

            EditorUtility.DisplayDialog(
                "Importación completada",
                result,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);

            EditorUtility.DisplayDialog(
                "Error de importación",
                exception.Message,
                "Aceptar"
            );
        }
    }

    private RiddleImportData CreateImportData(
        List<string> row,
        int csvRowNumber
    )
    {
        const int expectedColumnCount = 10;

        if (
            row == null ||
            row.Count < expectedColumnCount
        )
        {
            Debug.LogError(
                $"La fila {csvRowNumber} tiene " +
                $"{row?.Count ?? 0} columnas, pero se esperaban " +
                $"{expectedColumnCount}."
            );

            return null;
        }

        string id = row[0].Trim();
        string category = row[1].Trim();
        string difficultyText = row[2].Trim();
        string answer = row[3].Trim();
        string acceptedAnswersText = row[4].Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError(
                $"La fila {csvRowNumber} no tiene ID."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            Debug.LogError(
                $"La fila {csvRowNumber} no tiene categoría."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            Debug.LogError(
                $"La fila {csvRowNumber} no tiene respuesta."
            );

            return null;
        }

        string[] hints =
        {
            row[5].Trim(),
            row[6].Trim(),
            row[7].Trim(),
            row[8].Trim(),
            row[9].Trim()
        };

        for (int i = 0; i < hints.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(hints[i]))
            {
                Debug.LogError(
                    $"La fila {csvRowNumber} tiene vacía la pista {i + 1}."
                );

                return null;
            }
        }

        RiddleImportData data = new RiddleImportData
        {
            id = id,
            category = category,
            difficulty = ParseDifficulty(
                difficultyText,
                csvRowNumber
            ),
            answer = answer,
            acceptedAnswers = ParseAcceptedAnswers(
                acceptedAnswersText,
                answer
            ),
            hints = hints
        };

        return data;
    }

    private string[] ParseAcceptedAnswers(
        string acceptedAnswersText,
        string mainAnswer
    )
    {
        List<string> acceptedAnswers =
            new List<string>();

        acceptedAnswers.Add(mainAnswer);

        if (!string.IsNullOrWhiteSpace(acceptedAnswersText))
        {
            string[] variants =
                acceptedAnswersText.Split('|');

            foreach (string variant in variants)
            {
                string cleanVariant = variant.Trim();

                if (string.IsNullOrWhiteSpace(cleanVariant))
                {
                    continue;
                }

                bool alreadyExists = false;

                foreach (string existingAnswer in acceptedAnswers)
                {
                    if (
                        string.Equals(
                            existingAnswer,
                            cleanVariant,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    acceptedAnswers.Add(cleanVariant);
                }
            }
        }

        return acceptedAnswers.ToArray();
    }

    private RiddleDifficulty ParseDifficulty(
        string difficultyText,
        int csvRowNumber
    )
    {
        string normalizedDifficulty =
            difficultyText
                .Trim()
                .ToLowerInvariant();

        switch (normalizedDifficulty)
        {
            case "easy":
            case "facil":
            case "fácil":
            case "1":
                return RiddleDifficulty.Easy;

            case "medium":
            case "medio":
            case "media":
            case "normal":
            case "2":
                return RiddleDifficulty.Medium;

            case "hard":
            case "dificil":
            case "difícil":
            case "3":
                return RiddleDifficulty.Hard;

            default:
                Debug.LogWarning(
                    $"Fila {csvRowNumber}: dificultad desconocida " +
                    $"'{difficultyText}'. Se utilizará Medium."
                );

                return RiddleDifficulty.Medium;
        }
    }

    private RiddleSO CreateOrUpdateRiddleAsset(
        RiddleImportData importData,
        ref int createdCount,
        ref int updatedCount
    )
    {
        string safeFileName =
            SanitizeFileName(importData.id);

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            Debug.LogError(
                $"El ID '{importData.id}' no puede convertirse " +
                "en un nombre de archivo válido."
            );

            return null;
        }

        string normalizedFolder =
            targetFolder.Replace("\\", "/");

        string assetPath =
            $"{normalizedFolder}/{safeFileName}.asset";

        RiddleSO riddle =
            AssetDatabase.LoadAssetAtPath<RiddleSO>(
                assetPath
            );

        if (riddle == null)
        {
            riddle = CreateInstance<RiddleSO>();

            AssetDatabase.CreateAsset(
                riddle,
                assetPath
            );

            createdCount++;
        }
        else
        {
            updatedCount++;
        }

        riddle.name = safeFileName;
        riddle.id = importData.id;
        riddle.category = importData.category;
        riddle.difficulty = importData.difficulty;
        riddle.answer = importData.answer;
        riddle.acceptedAnswers =
            importData.acceptedAnswers;
        riddle.hints = importData.hints;

        EditorUtility.SetDirty(riddle);

        return riddle;
    }

    private void UpdateDatabase(
        List<RiddleSO> importedRiddles
    )
    {
        if (database == null)
        {
            Debug.LogError(
                "No hay una base de datos disponible."
            );

            return;
        }

        database.riddles.Clear();

        foreach (RiddleSO riddle in importedRiddles)
        {
            if (riddle != null)
            {
                database.riddles.Add(riddle);
            }
        }

        database.riddles.Sort(
            (first, second) =>
                string.Compare(
                    first.id,
                    second.id,
                    StringComparison.OrdinalIgnoreCase
                )
        );

        EditorUtility.SetDirty(database);
    }

    private RiddleDatabaseSO LoadOrCreateDatabase()
    {
        string normalizedPath =
            databasePath.Replace("\\", "/");

        RiddleDatabaseSO existingDatabase =
            AssetDatabase.LoadAssetAtPath<RiddleDatabaseSO>(
                normalizedPath
            );

        if (existingDatabase != null)
        {
            return existingDatabase;
        }

        string databaseFolder =
            Path.GetDirectoryName(normalizedPath)
                ?.Replace("\\", "/");

        if (string.IsNullOrWhiteSpace(databaseFolder))
        {
            Debug.LogError(
                "La ruta de la base de datos no es válida."
            );

            return null;
        }

        EnsureFolder(databaseFolder);

        RiddleDatabaseSO newDatabase =
            CreateInstance<RiddleDatabaseSO>();

        AssetDatabase.CreateAsset(
            newDatabase,
            normalizedPath
        );

        EditorUtility.SetDirty(newDatabase);

        Debug.Log(
            $"Base de datos creada: {normalizedPath}"
        );

        return newDatabase;
    }

    private void EnsureFolder(string folderPath)
    {
        folderPath =
            folderPath.Replace("\\", "/");

        if (!folderPath.StartsWith("Assets"))
        {
            throw new Exception(
                $"La carpeta debe estar dentro de Assets: {folderPath}"
            );
        }

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] folders =
            folderPath.Split('/');

        string currentPath = "Assets";

        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath =
                $"{currentPath}/{folders[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    folders[i]
                );
            }

            currentPath = nextPath;
        }
    }

    private string SanitizeFileName(string id)
    {
        string fileName = id
            .Trim()
            .ToLowerInvariant();

        fileName = RemoveDiacritics(fileName);

        fileName = Regex.Replace(
            fileName,
            @"[^a-z0-9_\-]+",
            "_"
        );

        fileName = Regex.Replace(
            fileName,
            @"_+",
            "_"
        );

        return fileName.Trim('_');
    }

    private string RemoveDiacritics(string text)
    {
        string normalizedText =
            text.Normalize(
                NormalizationForm.FormD
            );

        StringBuilder result =
            new StringBuilder();

        foreach (char character in normalizedText)
        {
            System.Globalization.UnicodeCategory category =
                System.Globalization.CharUnicodeInfo
                    .GetUnicodeCategory(character);

            if (
                category !=
                System.Globalization.UnicodeCategory
                    .NonSpacingMark
            )
            {
                result.Append(character);
            }
        }

        return result
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private bool IsEmptyRow(List<string> row)
    {
        if (row == null || row.Count == 0)
        {
            return true;
        }

        foreach (string cell in row)
        {
            if (!string.IsNullOrWhiteSpace(cell))
            {
                return false;
            }
        }

        return true;
    }

    private List<List<string>> ParseCsv(
        string csvText
    )
    {
        List<List<string>> rows =
            new List<List<string>>();

        List<string> currentRow =
            new List<string>();

        StringBuilder currentField =
            new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char currentChar = csvText[i];

            if (currentChar == '"')
            {
                bool isEscapedQuote =
                    insideQuotes &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '"';

                if (isEscapedQuote)
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (
                currentChar == ',' &&
                !insideQuotes
            )
            {
                currentRow.Add(
                    currentField.ToString()
                );

                currentField.Clear();
            }
            else if (
                (
                    currentChar == '\n' ||
                    currentChar == '\r'
                ) &&
                !insideQuotes
            )
            {
                if (
                    currentChar == '\r' &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '\n'
                )
                {
                    i++;
                }

                currentRow.Add(
                    currentField.ToString()
                );

                currentField.Clear();

                rows.Add(currentRow);

                currentRow =
                    new List<string>();
            }
            else
            {
                currentField.Append(currentChar);
            }
        }

        currentRow.Add(
            currentField.ToString()
        );

        if (!IsEmptyRow(currentRow))
        {
            rows.Add(currentRow);
        }

        return rows;
    }
}