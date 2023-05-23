using System.Reflection;

namespace amcs_scanner_app.Helper;
/// <summary>
/// 
/// </summary>
public class BarcodeParser
{
    private List<ApplicationIdentifier> _applicationIdentifiers;

    /// <summary>
    /// FNC1 represented by ASCII 29 (Group Seperation Character)
    /// </summary>
    private static readonly char FNC1 = (char)29;

    private readonly int _maxCombinedBarcodes;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings"></param>
    public BarcodeParser(AppSettings settings)
    {
        _applicationIdentifiers = new List<ApplicationIdentifier>();
        _maxCombinedBarcodes = settings.MaxCombinedBarcodes;
        // ApplicationIdentiefier Properties aus settings auslesen (Reflection)
        settings.ApplicationIdentifiers.GetType().GetProperties().ToList().ForEach(prop =>
        {
            if (prop.PropertyType == typeof(ApplicationIdentifier))
                _applicationIdentifiers.Add((ApplicationIdentifier)prop.GetValue(settings.ApplicationIdentifiers));
        });
    }

    /// <summary>
    /// Reads input string -> check if first two or first three Characters match a Application Identifier. Loop unitl input is read completely or error occured.
    /// </summary>
    /// <param name="input"></param>
    /// <returns>List of BarcodeContent Objects</returns>
    /// <exception cref="InvalidDataException">Input could not pe parsed for some reason</exception>
    public List<BarcodeContent> ReadBarcode(string input)
    {
        bool done = false;
        var output = new List<BarcodeContent>();
        int iterations = 0;
        // Parse the string in loop until last code was identified
        while (!done)
        {
            iterations++;
            if (iterations > _maxCombinedBarcodes)
                throw new InvalidDataException("Scanned Code exceeds the limit of combined Codes!");

            // Find next AI
            var nextTwo = input.Substring(0, 2);
            var nextThree = input.Substring(0, 3);

            if (_applicationIdentifiers.Any(ai => ai.Identifier == nextTwo))
            {
                // AI found for first two chars
                var applicationIdentifier = _applicationIdentifiers.First(ai => ai.Identifier == nextTwo);

                input = input.Substring(2);

                if (applicationIdentifier.FixedLength)
                {
                    if (applicationIdentifier.ValueLength is null)
                        throw new InvalidDataException("The ApplicationIdentifier '" + applicationIdentifier.Description 
                            + "' (" + applicationIdentifier.Identifier + ") has fixedLength == true but no ValueLength is defined!");

                    if (input.Count() == applicationIdentifier.ValueLength)
                    {
                        output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = nextTwo, Value = input });
                        done = true; break;
                    }
                    else
                    {
                        if (input.Count() < applicationIdentifier.ValueLength)
                            throw new InvalidDataException("Barcode Value is shorter than the FixedLength of Identifier!");

                        var value = input.Substring(0, applicationIdentifier.ValueLength.Value);
                        input = input.Substring(applicationIdentifier.ValueLength.Value);
                        output.Add(new BarcodeContent
                        {
                            Description = applicationIdentifier.Description,
                            Identifier = applicationIdentifier.Identifier,
                            Value = value
                        });
                        continue;
                    }
                }
                else
                {
                    if (input.Any(c => c == FNC1))
                    {
                        // ---> This is not the last code in input
                        var value = input.Substring(0, input.IndexOf(FNC1));
                        input = input.Substring(input.IndexOf(FNC1) + 1);
                        output.Add(new BarcodeContent
                        {
                            Description = applicationIdentifier.Description,
                            Identifier = applicationIdentifier.Identifier,
                            Value = value
                        });
                        continue;
                    }
                    else
                    {
                        // Last code in input
                        output.Add(new BarcodeContent
                        {
                            Description = applicationIdentifier.Description,
                            Identifier = applicationIdentifier.Identifier,
                            Value = input
                        });
                        done = true; break;
                    }
                }
            }
            else if (_applicationIdentifiers.Any(ai => ai.Identifier == nextThree))
            {
                // AI found for first three chars
                var applicationIdentifier = _applicationIdentifiers.First(ai => ai.Identifier == nextThree);

                input = input.Substring(3);

                if (applicationIdentifier.FixedLength)
                {
                    if (applicationIdentifier.ValueLength is null)
                        throw new InvalidDataException("The ApplicationIdentifier '" + applicationIdentifier.Description + "' (" + applicationIdentifier.Identifier + ") has fixedLength == true but no ValueLength is defined!");

                    if (applicationIdentifier.HasDecimalPoint)
                    {
                        var decimalPoint = int.Parse(input[0].ToString());
                        input = input.Substring(1);

                        if (input.Count() == applicationIdentifier.ValueLength)
                        {
                            var number = double.Parse(input);
                            var value = number / Math.Pow(10, decimalPoint);
                            output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = applicationIdentifier.Identifier, Value = value.ToString() });
                            done = true; break;
                        }
                        else
                        {
                            if (input.Count() < applicationIdentifier.ValueLength)
                                throw new InvalidDataException("Barcode Value is shorter than the FixedLength of Identifier!");

                            var code = input.Substring(0, applicationIdentifier.ValueLength.Value);
                            input = input.Substring(applicationIdentifier.ValueLength.Value);
                            var number = double.Parse(code);
                            var value = number / Math.Pow(10, decimalPoint);
                            output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = applicationIdentifier.Identifier, Value = value.ToString() });
                        }
                    }
                    else
                    {
                        if (input.Count() == applicationIdentifier.ValueLength)
                        {
                            output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = nextThree, Value = input });
                            done = true; break;
                        }
                        else
                        {
                            if (input.Count() < applicationIdentifier.ValueLength)
                                throw new InvalidDataException("Barcode Value is shorter than the FixedLength of Identifier!");

                            var value = input.Substring(0, applicationIdentifier.ValueLength.Value);
                            input = input.Substring(applicationIdentifier.ValueLength.Value);
                            output.Add(new BarcodeContent
                            {
                                Description = applicationIdentifier.Description,
                                Identifier = applicationIdentifier.Identifier,
                                Value = value
                            });
                            continue;
                        }
                    }
                }
                else
                {
                    if (applicationIdentifier.HasDecimalPoint)
                    {
                        // Not the last code in input -> split at FNC1
                        var decimalPoint = int.Parse(input[0].ToString());
                        input = input.Substring(1);
                        if (input.Any(c => c == FNC1))
                        {
                            var code = input.Substring(0, input.IndexOf(FNC1));
                            input = input.Substring(input.IndexOf(FNC1) + 1);
                            var value = int.Parse(code) / Math.Pow(10, decimalPoint);
                            output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = applicationIdentifier.Identifier, Value = value.ToString() });
                            continue;
                        }
                        else
                        {
                            // Last code in input
                            var value = int.Parse(input) / Math.Pow(10, decimalPoint);
                            output.Add(new BarcodeContent { Description = applicationIdentifier.Description, Identifier = applicationIdentifier.Identifier, Value = value.ToString() });
                            done = true; break;
                        }
                    }
                    else
                    {
                        if (input.Any(c => c == FNC1))
                        {
                            // Not the last code in input -> split at FNC1
                            var value = input.Substring(0, input.IndexOf(FNC1));
                            input = input.Substring(input.IndexOf(FNC1) + 1);
                            output.Add(new BarcodeContent
                            {
                                Description = applicationIdentifier.Description,
                                Identifier = applicationIdentifier.Identifier,
                                Value = value
                            });
                            continue;
                        }
                        else
                        {
                            // No FNC1 in input -> Last Code
                            output.Add(new BarcodeContent
                            {
                                Description = applicationIdentifier.Description,
                                Identifier = applicationIdentifier.Identifier,
                                Value = input
                            });
                            done = true; break;
                        }
                    }
                }
            }
            else
                throw new InvalidOperationException("Input string could not be parsed. No ApplicationIdentifier for (" + nextTwo + ") or (" + nextThree + ") configured.");
        }
        return output;
    }
}
