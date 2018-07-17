﻿using System.Collections.Generic;
using System.Threading.Tasks;
using FlickrNet;

namespace ImageGallery.FlickrService
{
    public interface ISearchService
    {
        Task<PhotoInfo> GetPhotoInfo(string photoId);

        IList<PhotoInfo> GetPhotoInfoList(IEnumerable<string> photoIdList);

        IList<Photo> SearchPhotos(PhotoSearchOptions photoSearchOptions);
    }
}