using System;
using System.Collections.Generic;
using HotChocolate.Language;

namespace HotChocolate.Types.Descriptors.Definitions
{
    public class TypeDefinitionBase<T>
        : DefinitionBase<T>
        , IHasRuntimeType
        , IHasDirectiveDefinition
        where T : class, ISyntaxNode
    {
        protected TypeDefinitionBase() { }

        private Type _clrType;

        /// <summary>
        /// Gets or sets the .net type representation of this type.
        /// </summary>
        public virtual Type RuntimeType
        {
            get => _clrType;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _clrType = value;
            }
        }

        /// <summary>
        /// Gets the list of directives that are annotated to this type.
        /// </summary>
        public IList<DirectiveDefinition> Directives { get; } =
            new List<DirectiveDefinition>();
    }
}
