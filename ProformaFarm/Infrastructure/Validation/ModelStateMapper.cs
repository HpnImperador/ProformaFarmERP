using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProformaFarm.API.Infrastructure.Validation;

public static class ModelStateMapper
{
    public static IReadOnlyDictionary<string, string[]> ToFieldErrors(ModelStateDictionary modelState)
    {
        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0) continue;

            var field = string.IsNullOrWhiteSpace(key) ? "request" : key;

            var messages = entry.Errors
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                .ToArray();

            dict[field] = messages;
        }

        return dict;
    }
}
