// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Translates accesses to relationship members into projections or joins
    /// </summary>
    public class RelationshipBinder : DbExpressionVisitor
    {
        private readonly QueryMapper _mapper;
        private readonly QueryMapping _mapping;
        private readonly QueryLanguage _language;
        private Expression _currentFrom;

        private RelationshipBinder(QueryMapper mapper)
        {
            _mapper = mapper;
            _mapping = mapper.Mapping;
            _language = mapper.Translator.Linguist.Language;
        }

        public static Expression Bind(QueryMapper mapper, Expression expression)
        {
            return new RelationshipBinder(mapper).Visit(expression);
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            var saveCurrentFrom = _currentFrom;
            _currentFrom = VisitSource(select.From);
            try
            {
                var where = Visit(select.Where);
                var orderBy = VisitOrderBy(select.OrderBy);
                var groupBy = VisitExpressionList(select.GroupBy);
                var skip = Visit(select.Skip);
                var take = Visit(select.Take);
                var columns = VisitColumnDeclarations(select.Columns);
                if (_currentFrom != select.From
                    || where != select.Where
                    || orderBy != select.OrderBy
                    || groupBy != select.GroupBy
                    || take != select.Take
                    || skip != select.Skip
                    || columns != select.Columns
                    )
                {
                    return new SelectExpression(select.Alias, columns, _currentFrom, where, orderBy, groupBy, select.IsDistinct, skip, take, select.IsReverse);
                }
                return select;
            }
            finally
            {
                _currentFrom = saveCurrentFrom;
            }
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            var source = Visit(m.Expression);
            var ex = source as EntityExpression;

            if (ex != null && _mapping.IsRelationship(ex.Entity, m.Member))
            {
                var projection = (ProjectionExpression)Visit(_mapper.GetMemberExpression(source, ex.Entity, m.Member));
                if (_currentFrom != null && _mapping.IsSingletonRelationship(ex.Entity, m.Member))
                {
                    // convert singleton associations directly to OUTER APPLY
                    projection = _language.AddOuterJoinTest(projection);
                    Expression newFrom = new JoinExpression(JoinType.OuterApply, _currentFrom, projection.Select, null);
                    _currentFrom = newFrom;
                    return projection.Projector;
                }
                return projection;
            }

            var result = QueryBinder.BindMember(source, m.Member);
            var mex = result as MemberExpression;
            if (mex != null && mex.Member == m.Member && mex.Expression == m.Expression)
            {
                return m;
            }
            return result;
        }
    }
}
