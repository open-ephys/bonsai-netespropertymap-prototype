
Experimental repository for prototype bonsai node to map nested properties of nodes

Since it depends on IArgumentBuilder which is an internal class of Bonsai.Core, requires for now
a modified core with `[assembly: InternalsVisibleTo("Test.ClassPropertyMap")]` on
AssemblyInfo.cs

Names of classes pending to be changed