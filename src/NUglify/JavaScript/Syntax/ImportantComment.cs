﻿using NUglify.JavaScript.Visitors;

namespace NUglify.JavaScript.Syntax
{
    public class ImportantComment : AstNode
    {
        public string Comment { get; set; }

        // this is for determining if a node in a block AFTER a return/break/continue should be removed. We don't want to remove an important comment, so SAY it's a declaration.
        public override bool IsDeclaration => true;

        public ImportantComment(SourceContext context) : base(context)
        {
            Comment = Context.Code;
        }

        public override void Accept(IVisitor visitor)
        {
	        visitor?.Visit(this);
        }
    }
}
