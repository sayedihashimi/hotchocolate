using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace HotChocolate.Data.Filters.Expressions
{
    public class QueryableFilterProvider
        : FilterProvider<QueryableFilterContext>
    {
        public const string ContextArgumentNameKey = "FilterArgumentName";
        public const string ContextVisitFilterArgumentKey = nameof(VisitFilterArgument);
        public const string SkipFilteringKey = "SkipFiltering";


        public QueryableFilterProvider()
        {
        }

        public QueryableFilterProvider(
            Action<IFilterProviderDescriptor<QueryableFilterContext>> configure)
            : base(configure)
        {
        }

        protected virtual FilterVisitor<QueryableFilterContext, Expression> Visitor { get; } =
            new FilterVisitor<QueryableFilterContext, Expression>(new QueryableCombinator());

        public override FieldMiddleware CreateExecutor<TEntityType>(NameString argumentName)
        {
            return next => context => ExecuteAsync(next, context);

            async ValueTask ExecuteAsync(
                FieldDelegate next,
                IMiddlewareContext context)
            {
                // first we let the pipeline run and produce a result.
                await next(context).ConfigureAwait(false);

                // next we get the filter argument.
                IInputField argument = context.Field.Arguments[argumentName];
                IValueNode filter = context.ArgumentLiteral<IValueNode>(argumentName);

                // if no filter is defined we can stop here and yield back control.
                if (filter.IsNull() ||
                    (context.LocalContextData.TryGetValue(
                            SkipFilteringKey,
                            out object? skipObject) &&
                        skipObject is bool skip &&
                        skip))
                {
                    return;
                }

                IQueryable<TEntityType>? source = null;

                if (context.Result is IQueryable<TEntityType> q)
                {
                    source = q;
                }
                else if (context.Result is IEnumerable<TEntityType> e)
                {
                    source = e.AsQueryable();
                }

                if (source != null &&
                    argument.Type is IFilterInputType filterInput &&
                    context.Field.ContextData.TryGetValue(
                        ContextVisitFilterArgumentKey,
                        out object? executorObj) &&
                    executorObj is VisitFilterArgument executor)
                {
                    QueryableFilterContext visitorContext = executor(
                        filter,
                        filterInput,
                        source is EnumerableQuery);

                    // compile expression tree
                    if (visitorContext.TryCreateLambda(
                        out Expression<Func<TEntityType, bool>>? where))
                    {
                        context.Result = source.Where(where);
                    }
                    else
                    {
                        if (visitorContext.Errors.Count > 0)
                        {
                            context.Result = Array.Empty<TEntityType>();
                            foreach (IError error in visitorContext.Errors)
                            {
                                context.ReportError(error.WithPath(context.Path));
                            }
                        }
                    }
                }
            }
        }

        public override void ConfigureField(
            NameString argumentName,
            IObjectFieldDescriptor descriptor)
        {
            QueryableFilterContext VisitFilterArgumentExecutor(
                IValueNode valueNode,
                IFilterInputType filterInput,
                bool inMemory)
            {
                var visitorContext = new QueryableFilterContext(
                    filterInput,
                    inMemory);

                // rewrite GraphQL input object into expression tree.
                Visitor.Visit(valueNode, visitorContext);

                return visitorContext;
            }

            descriptor.ConfigureContextData(
                contextData =>
                {
                    contextData[ContextVisitFilterArgumentKey] =
                        (VisitFilterArgument)VisitFilterArgumentExecutor;
                    contextData[ContextArgumentNameKey] = argumentName;
                });
        }
    }

    public delegate QueryableFilterContext VisitFilterArgument(
        IValueNode filterValueNode,
        IFilterInputType filterInputType,
        bool inMemory);
}
