// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ResourceRepository.cs" company="Sitecore Corporation">
//   Copyright (c) Sitecore Corporation 1999-2016
// </copyright>
// <summary>
//   The resource repository.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Support.Commerce.Data.Products
{
    using System;
    using System.IO;
    using System.Linq;

    using Configuration;
    using Diagnostics;
    using Resources.Media;
    using SecurityModel;
    using Sitecore.Commerce;
    using Sitecore.Commerce.Data.Products;
    using Sitecore.Commerce.Entities.Products;
    using Sitecore.Data;
    using Sitecore.Data.Items;

    public class ResourceRepository : ArtifactRepository<Resource>
    {
        private readonly MediaProvider mediaProvider;

        public ResourceRepository([NotNull] MediaProvider mediaProvider)
        {
            Assert.ArgumentNotNull(mediaProvider, "mediaProvider");

            this.mediaProvider = mediaProvider;
        }

        public override Resource Save(Resource resource)
        {
            Assert.ArgumentNotNull(resource, "resource");

            if (string.IsNullOrEmpty(resource.Name) || string.IsNullOrEmpty(resource.ExternalId) ||
                resource.BinaryData == null) return resource;

            using (new SecurityDisabler())
            {
                // Get the place in the site tree where the new item must be inserted
                var repositoryItem = Database.GetItem(Path);
                if (repositoryItem == null)
                {
                    repositoryItem = Database.CreateItemPath(Path, Database.Templates[TemplateIDs.Folder],
                        Database.Templates[ID.Parse(Template)]);
                    Assert.IsNotNull(repositoryItem, Texts.CannotFindResourceRepositoryItemWith0, Path);
                }

                Save(repositoryItem, resource, false);
            }

            return resource;
        }

        public override void Save(Item root, Resource entity, bool moveToBucket)
        {
            Assert.ArgumentNotNull(root, "root");
            Assert.ArgumentNotNull(entity, "entity");

            if (!IsValid(root, entity)) return;

            var entityItem = GetEntityItem(root, GetEntityKey(root, entity));
            var entityItemImmedaiteRoot = GetEntityItemImmediateRoot(root, entity);

            if (entityItem == null)
            {
                entityItem = CreateEntityItem(root, entityItemImmedaiteRoot, entity, moveToBucket);
                UpdateEntityItem(entityItem, entity);
            }
            else
            {
                UpdateEntityItem(entityItem, entity);
                MoveToImmediateRoot(root, entityItemImmedaiteRoot, entityItem);
            }
        }

        protected override string GetEntityItemName(Item root, Resource entity)
        {
            return ItemUtil.ProposeValidItemName(entity.Name);
        }

        protected override void UpdateEntityItem(Item entityItem, Resource entity)
        {
            try
            {
                var extension = string.Empty;

                if (!string.IsNullOrEmpty(entity.MimeType))
                {
                    var mediaTypeConfig = mediaProvider.Config.GetMediaTypeConfigByMime(entity.MimeType);
                    extension = mediaTypeConfig.Extensions.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                        .First();
                }

                if (extension == "*")
                    extension = entity.MimeType.ToLowerInvariant().StartsWith("image", StringComparison.OrdinalIgnoreCase)
                        ? Settings.Media.DefaultImageFormat
                        : string.Empty;

                var mediaItem = new MediaItem(entityItem);
                var media = MediaManager.GetMedia(mediaItem);

                using (var stream = new MemoryStream(entity.BinaryData))
                {
                    media.SetStream(stream, extension);
                }

                using (new EditContext(entityItem))
                {
                    entityItem.Name = entity.Name;
                }

      }
            catch (Exception e)
            {
                Sitecore.Diagnostics.Log.Warn("Resource item externalId="+entity.ExternalId+" and Name="+entity.Name+" wasn't updated.", e, this);
            } 
        }
    }
}