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
    using System.Globalization;
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
    using Constants = Buckets.Util.Constants;

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
            var extension = string.Empty;

            if (!string.IsNullOrEmpty(entity.MimeType))
            {
                var mediaTypeConfig = mediaProvider.Config.GetMediaTypeConfigByMime(entity.MimeType);
                extension = mediaTypeConfig.Extensions.Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries)
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

        protected override Item ProcessEntityItem(Item entityItem, Resource entity)
        {
            var extension = string.Empty;

            if (!string.IsNullOrEmpty(entity.MimeType))
            {
                var mediaTypeConfig = mediaProvider.Config.GetMediaTypeConfigByMime(entity.MimeType);
                extension = mediaTypeConfig.Extensions.Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries)
                    .First();
            }

            if (extension == "*")
                extension = entity.MimeType.ToLowerInvariant().StartsWith("image", StringComparison.OrdinalIgnoreCase)
                    ? Settings.Media.DefaultImageFormat
                    : string.Empty;

            var path = entityItem.Paths.FullPath + "/" + entity.Name;
            var options = new MediaCreatorOptions
            {
                Database = Database,
                Destination = path,
                AlternateText = entity.Name,
                OverwriteExisting = true
            };
            var entityItemId = GetEntityItemId(entityItem, GetEntityKey(entityItem, entity));


            using (var stream = new MemoryStream(entity.BinaryData))
            {
                var fullPath = !string.IsNullOrEmpty(extension)
                    ? string.Format(CultureInfo.InvariantCulture, "{0}.{1}", path, extension)
                    : path;

                var result = mediaProvider.Creator.CreateFromStream(stream, fullPath, options);
                Assert.IsNotNull(result, Texts.FailedToCreateItemForTheEntityBeingSynchronized);

                using (new EditContext(result))
                {
                    result.Fields[ID.Parse(Constants.BucketableField)].Value = "1";
                }

                return result;
            }
        }

        protected override Item CreateEntityItem([NotNull] Item root, [NotNull] Item entityItemImmediateRoot,
            [NotNull] Resource entity, bool moveToBucket)
        {
            var result = ProcessEntityItem(root, entity);

            MoveItemIntoBucket(root, result, entity, moveToBucket);
            SetEntityItemStatistics(result, entity);

            return result;
        }
    }
}