// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Adds relationship to query results depending on policy
    /// </summary>
    public class RelationshipIncluder : DbExpressionVisitor
    {
        private readonly QueryMapper _mapper;
        private readonly QueryPolicy _policy;
        private ScopedDictionary<MemberInfo, bool> _includeScope = new ScopedDictionary<MemberInfo, bool>(null);

        private RelationshipIncluder(QueryMapper mapper)
        {
            _mapper = mapper;
            _policy = mapper.Translator.Police.Policy;
        }

        public static Expression Include(QueryMapper mapper, Expression expression)
        {
            return new RelationshipIncluder(mapper).Visit(expression);
        }

        protected override Expression VisitProjection(ProjectionExpression proj)
        {
            var projector = Visit(proj.Projector);
            return UpdateProjection(proj, proj.Select, projector, proj.Aggregator);
        }

        protected override Expression VisitEntity(EntityExpression entity)
        {
            var save = _includeScope;
            _includeScope = new ScopedDictionary<MemberInfo,bool>(_includeScope);
            try
            {
                if (_mapper.HasIncludedMembers(entity))
                {
                    entity = _mapper.IncludeMembers(
                        entity,
                        m =>
                        {
                            if (_includeScope.ContainsKey(m))
                            {
                                return false;
                            }
                            if (_policy.IsIncluded(m))
                            {
                                _includeScope.Add(m, true);
                                return true;
                            }
                            return false;
                        });
                }
                return base.VisitEntity(entity);
            }
            finally
            {
                _includeScope = save;
            }
        }
    }
}
