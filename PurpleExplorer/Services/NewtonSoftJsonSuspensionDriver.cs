using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Newtonsoft.Json;
using ReactiveUI;

namespace PurpleExplorer.Services;

public class NewtonsoftJsonSuspensionDriver(string file) : ISuspensionDriver
{
    private readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        Formatting = Formatting.Indented
    };

    public IObservable<Unit> InvalidateState()
    {
        if (File.Exists(file))
        {
            ConsoleColor initial = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Detected invalid state. Will move {file} to {file}.broken");
            Console.ForegroundColor = initial;

            try
            {
                if (File.Exists(file + ".broken")) File.Delete(file + ".broken");
                File.Move(file, file + ".broken");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving invalid state file: {ex.Message}");
            }
        }

        return Observable.Return(Unit.Default);
    }

    public IObservable<object> LoadState()
    {
        try
        {
            if (File.Exists(file))
            {
                string lines = File.ReadAllText(file);
                var state = JsonConvert.DeserializeObject<object>(lines, _settings);
                if (state != null) return Observable.Return(state);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading state from {file}: {ex.Message}");
        }

        string backupFile = file + ".backup";
        if (File.Exists(backupFile))
            try
            {
                Console.WriteLine($"Attempting to load state from backup: {backupFile}");
                string lines = File.ReadAllText(backupFile);
                var state = JsonConvert.DeserializeObject<object>(lines, _settings);
                if (state != null) return Observable.Return(state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading state from backup {backupFile}: {ex.Message}");
            }

        return Observable.Throw<object>(new FileNotFoundException(file));
    }

    public IObservable<Unit> SaveState(object state)
    {
        string lines = JsonConvert.SerializeObject(state, _settings);
        Console.WriteLine($"Saving state: will write {lines.Length} lines to {file}");

        string backupFile = file + ".backup";
        string tempFile = file + ".tmp";

        try
        {
            File.WriteAllText(tempFile, lines);
            if (File.Exists(file)) File.Copy(file, backupFile, true);
            File.Move(tempFile, file, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving state: {ex.Message}");
        }

        return Observable.Return(Unit.Default);
    }
}
