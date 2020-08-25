﻿using System;
using UnityEngine.Perception.Randomization.Parameters;

namespace UnityEngine.Perception.Randomization.ParameterBehaviours
{
    /// <summary>
    /// Used to apply sampled parameter values to a particular GameObject, Component, and property.
    /// Typically managed by a parameter configuration.
    /// </summary>
    [Serializable]
    public class ParameterTarget
    {
        [SerializeField] internal Component component;
        [SerializeField] internal string propertyName = string.Empty;
        [SerializeField] internal FieldOrProperty fieldOrProperty = FieldOrProperty.Field;
        [SerializeField] internal ParameterApplicationFrequency applicationFrequency = ParameterApplicationFrequency.EveryIteration;

        /// <summary>
        /// Assigns a new target
        /// </summary>
        /// <param name="targetComponent">The target component on the target GameObject</param>
        /// <param name="fieldOrPropertyName">The name of the property to apply the parameter to</param>
        /// <param name="frequency">How often to apply the parameter to its target</param>
        public void AssignNewTarget(
            Component targetComponent,
            string fieldOrPropertyName,
            ParameterApplicationFrequency frequency)
        {
            component = targetComponent;
            propertyName = fieldOrPropertyName;
            applicationFrequency = frequency;
            var componentType = component.GetType();
            fieldOrProperty = componentType.GetField(fieldOrPropertyName) != null
                ? FieldOrProperty.Field
                : FieldOrProperty.Property;
        }

        internal void Clear()
        {
            component = null;
            propertyName = string.Empty;
        }

        internal void ApplyValueToTarget(object value)
        {
            var componentType = component.GetType();
            if (fieldOrProperty == FieldOrProperty.Field)
            {
                var field = componentType.GetField(propertyName);
                if (field == null)
                    throw new ParameterValidationException(
                        $"Component type {componentType.Name} does not have a field named {propertyName}");
                field.SetValue(component, value);
            }
            else
            {
                var property = componentType.GetProperty(propertyName);
                if (property == null)
                    throw new ParameterValidationException(
                        $"Component type {componentType.Name} does not have a property named {propertyName}");
                property.SetValue(component, value);
            }
        }

        internal void Validate()
        {
            if (component == null)
                throw new ParameterValidationException("Null target component");
            if (string.IsNullOrEmpty(propertyName))
                throw new ParameterValidationException("Invalid target property");
        }
    }

    /// <summary>
    /// How often to apply a new sample to a parameter's target
    /// </summary>
    public enum ParameterApplicationFrequency
    {
        /// <summary>
        /// Applies a parameter at the beginning of every iteration
        /// </summary>
        EveryIteration,

        /// <summary>
        /// Applies a parameter at the beginning of every frame
        /// </summary>
        EveryFrame
    }

    enum FieldOrProperty
    {
        Field, Property
    }
}
