﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace DriveOnSurface
{
    /**
    * Interface pour les objets dessinables.
    **/
    interface IDrawableObject
    {
        String getID();

        void Draw(SpriteBatch sb);

        void LoadContent(ContentManager theContentManager);
    }
}
