using System;
using System.Linq.Expressions;


namespace SimplePathfinding {

class Utils
{
	public static Func<TObject, TValue> FieldGetter<TObject, TValue>(string fieldName)
	{
		ParameterExpression param = Expression.Parameter(typeof(TObject), "arg");
		MemberExpression member = Expression.Field(param, fieldName);
		LambdaExpression lambda = Expression.Lambda<Func<TObject, TValue>>(member, param);
		return (Func<TObject, TValue>)lambda.Compile();
	}

	// Note: Expression.Assign is available on .NET framework 4. not 3.5.
}

}
