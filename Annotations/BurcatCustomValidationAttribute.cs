using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class BurcatCustomValidationAttribute : ValidationAttribute
    {
        private ParameterInfo ObjectInfo { get; }
        private Delegate Delegate { get; }

        public BurcatCustomValidationAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type validatorType, string validationMethod)
        {
            if (validatorType.GetMethod(validationMethod, BindingFlags.Public | BindingFlags.Static) is MethodInfo method)
            {
                if (method.ReturnType == typeof(ValidationResult))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1) throw new InvalidOperationException($"A {nameof(BurcatCustomValidationAttribute)} needs methods that have a single object, of any type, which might be nullable, as parameter.");
                    else
                    {
                        ObjectInfo = parameters[0];
                        Delegate = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType([ObjectInfo.ParameterType, typeof(ValidationResult)]), method);
                    }
                }
                else throw new InvalidOperationException($"A {nameof(BurcatCustomValidationAttribute)} needs methods than return {typeof(ValidationResult)}, which might be null.");
            }
            else throw new NullReferenceException($"No static method {validationMethod} in {validatorType.Name} was found.");
        }

        public override bool IsValid(object? value)
        {
            if (Transformable.TryDynamicCast(value, ObjectInfo, out object? pValue))
            {
                ValidationResult? result = (ValidationResult?)Delegate.DynamicInvoke(pValue);
                return result is null || result == ValidationResult.Success;
            }
            else return false;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (Transformable.TryDynamicCast(value, ObjectInfo, out object? result)) return (ValidationResult?)Delegate.DynamicInvoke(result);
            else return new("The specified object is not of the expected validation type.");
        }
    }
}
