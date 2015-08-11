﻿using Shapeshifter.Core.Data;
using Shapeshifter.Core.Factories.Interfaces;
using Shapeshifter.UserInterface.WindowsDesktop.Controls.Clipboard.Designer.Facades;
using Shapeshifter.UserInterface.WindowsDesktop.Properties;
using Shapeshifter.UserInterface.WindowsDesktop.Services.Interfaces;

namespace Shapeshifter.UserInterface.WindowsDesktop.Controls.Clipboard.Designer.Factories
{
    class DesignerFileDataSourceService : IDataSourceService
    {
        private readonly IImagePersistenceService imagePersistenceService;

        public DesignerFileDataSourceService(IImagePersistenceService imagePersistenceService)
        {
            this.imagePersistenceService = imagePersistenceService;
        }

        public IDataSource GetDataSource()
        {
            return new DesignerDataSourceFacade(imagePersistenceService)
            {
                Text = "My pictures",
                Icon = Resources.FileDataSourceIcon
            };
        }
    }
}
